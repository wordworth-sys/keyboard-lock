// KeyboardLock — 临时锁住键盘，鼠标与滚轮完全不受影响。
// MIT License · https://github.com/wordworth-sys/keyboard-lock
//
// 实现要点：
//   1. 只安装 WH_KEYBOARD_LL 低层键盘钩子，从不碰鼠标。
//   2. 快捷键按“虚拟键码”匹配，而不是按字符匹配，避免 Ctrl/Alt 组合被
//      键盘布局转换吃掉（这正是 Keylock v1.0.0 失效的原因）。
//   3. 锁定期间：非修饰键按下/抬起全部吞掉；修饰键只吞按下、放行抬起。
//      放行抬起是关键——锁定瞬间 Ctrl/Alt 的按下已经被系统接收，若连抬起
//      也吞掉，系统会一直认为 Ctrl 处于按下状态，滚轮就变成 Ctrl+滚轮。
//   4. 快捷键按住不放（自动重复）只触发一次。

using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

public class KeyboardLockForm : Form
{
    private const int WH_KEYBOARD_LL = 13;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_MENU = 0x12;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_U = 0x55;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int SW_RESTORE = 9;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static KeyboardLockForm current;
    private static IntPtr hookId = IntPtr.Zero;
    private static readonly LowLevelKeyboardProc hookProc = HookCallback;

    // 修饰键状态由钩子自己维护：锁定期间按键被吞，操作系统的状态不可信。
    private static bool ctrlDown;
    private static bool altDown;
    private static bool uDown;

    private bool locked;
    private bool hookFailed;

    private Label lblStatus;
    private Label lblHint;
    private Button btnToggle;

    public KeyboardLockForm()
    {
        current = this;

        // 窗口是 DPI 感知的，字体尺寸会自动按 DPI 换算，但硬编码的像素布局不会，
        // 所以这里按当前 DPI 手动缩放所有控件位置，避免高分屏上窗口变得很小或错位。
        AutoScaleMode = AutoScaleMode.None;
        float s = DpiScale();

        Text = "键盘锁";
        ClientSize = new Size(Px(360, s), Px(186, s));
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // 取不到图标不影响功能
        }

        lblStatus = new Label();
        lblStatus.SetBounds(Px(16, s), Px(16, s), Px(328, s), Px(34, s));
        lblStatus.Font = new Font("Microsoft YaHei UI", 15F, FontStyle.Bold);
        lblStatus.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(lblStatus);

        btnToggle = new Button();
        btnToggle.SetBounds(Px(16, s), Px(58, s), Px(328, s), Px(48, s));
        btnToggle.FlatStyle = FlatStyle.Flat;
        btnToggle.FlatAppearance.BorderColor = Color.Gainsboro;
        btnToggle.Click += delegate { Toggle(); };
        Controls.Add(btnToggle);

        lblHint = new Label();
        lblHint.SetBounds(Px(16, s), Px(116, s), Px(328, s), Px(52, s));
        lblHint.ForeColor = Color.Gray;
        lblHint.TextAlign = ContentAlignment.MiddleCenter;
        lblHint.Text = "解锁快捷键：Ctrl + Alt + U\n鼠标与滚轮始终可用，也可直接点上面的按钮";
        Controls.Add(lblHint);

        UpdateUi();
    }

    private static int Px(int value, float scale)
    {
        return (int)Math.Round(value * scale);
    }

    private static float DpiScale()
    {
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
        {
            return g.DpiX / 96F;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (hookId != IntPtr.Zero)
            return;

        IntPtr hMod = GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName);
        hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, hMod, 0);

        if (hookId == IntPtr.Zero)
        {
            hookFailed = true;
            btnToggle.Enabled = false;
            lblStatus.Text = "无法安装键盘钩子";
            lblStatus.ForeColor = Color.Firebrick;
            lblHint.Text = "错误码 " + Marshal.GetLastWin32Error() + "，请重试或换一台机器";
            MessageBox.Show(
                "键盘钩子安装失败（错误码 " + Marshal.GetLastWin32Error() + "），本程序无法锁定键盘。",
                "键盘锁", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        locked = false;
        if (hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
        }
        base.OnFormClosing(e);
    }

    private void Toggle()
    {
        if (hookFailed)
            return;

        locked = !locked;
        UpdateUi();
    }

    private void UpdateUi()
    {
        if (locked)
        {
            lblStatus.Text = "键盘已锁定";
            lblStatus.ForeColor = Color.Firebrick;
            btnToggle.Text = "解锁（Ctrl + Alt + U）";
            Text = "键盘锁 — 已锁定";
        }
        else
        {
            lblStatus.Text = "键盘未锁定";
            lblStatus.ForeColor = Color.SeaGreen;
            btnToggle.Text = "锁定键盘";
            Text = "键盘锁";
        }

        ApplyTopMost(locked);
    }

    // 只改 Z 序，不激活窗口，避免锁定/解锁时把焦点从你正在用的程序抢走。
    private void ApplyTopMost(bool on)
    {
        if (!IsHandleCreated)
            return;

        SetWindowPos(
            Handle,
            on ? HWND_TOPMOST : HWND_NOTOPMOST,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private static bool IsCtrl(int vk)
    {
        return vk == VK_CONTROL || vk == VK_LCONTROL || vk == VK_RCONTROL;
    }

    private static bool IsAlt(int vk)
    {
        return vk == VK_MENU || vk == VK_LMENU || vk == VK_RMENU;
    }

    private static bool IsModifier(int vk)
    {
        return IsCtrl(vk) || IsAlt(vk)
            || vk == VK_SHIFT || vk == VK_LSHIFT || vk == VK_RSHIFT
            || vk == VK_LWIN || vk == VK_RWIN;
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || current == null)
            return CallNextHookEx(hookId, nCode, wParam, lParam);

        try
        {
            int msg = wParam.ToInt32();
            bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

            if (!isDown && !isUp)
                return CallNextHookEx(hookId, nCode, wParam, lParam);

            int vk = Marshal.ReadInt32(lParam);

            if (IsCtrl(vk))
                ctrlDown = isDown;
            else if (IsAlt(vk))
                altDown = isDown;

            // 快捷键 Ctrl + Alt + U。按住不放时系统会连发按下事件，只认第一次。
            if (vk == VK_U)
            {
                if (isUp)
                {
                    uDown = false;
                }
                else if (!uDown)
                {
                    uDown = true;
                    if (ctrlDown && altDown)
                    {
                        current.Toggle();
                        return (IntPtr)1;
                    }
                }
            }

            // 修饰键：吞“按下”，放行“抬起”。
            // 这是修复滚轮异常的关键（见文件头说明）。
            if (IsModifier(vk))
            {
                if (isUp)
                    return CallNextHookEx(hookId, nCode, wParam, lParam);
                return current.locked ? (IntPtr)1 : CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            if (current.locked)
                return (IntPtr)1;

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }
        catch
        {
            // 钩子回调里抛异常会被系统直接摘钩，任何异常都必须放行按键
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }
    }

    private static void ActivateExistingInstance()
    {
        try
        {
            int self = Process.GetCurrentProcess().Id;
            foreach (Process p in Process.GetProcessesByName("KeyboardLock"))
            {
                if (p.Id == self)
                    continue;

                IntPtr h = p.MainWindowHandle;
                if (h == IntPtr.Zero)
                    continue;

                ShowWindow(h, SW_RESTORE);
                SetForegroundWindow(h);
                return;
            }
        }
        catch
        {
            // 唤起失败也不影响已经运行的那个实例
        }
    }

    [STAThread]
    public static void Main()
    {
        bool createdNew;
        using (Mutex mutex = new Mutex(true, @"Local\KeyboardLock_SingleInstance", out createdNew))
        {
            if (!createdNew)
            {
                ActivateExistingInstance();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new KeyboardLockForm());
        }
    }
}
