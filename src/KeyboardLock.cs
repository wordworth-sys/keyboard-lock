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
//   5. 关闭窗口只收进托盘，必须从托盘菜单「退出」才真正结束进程。

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

#region 绘制辅助

internal static class Round
{
    public static GraphicsPath Path(Rectangle r, int radius)
    {
        GraphicsPath p = new GraphicsPath();
        int d = Math.Max(2, radius * 2);
        if (d >= r.Width) d = r.Width - 1;
        if (d >= r.Height) d = r.Height - 1;

        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

internal sealed class StatusPill : Control
{
    public Color Fill = Color.White;
    public Color Ink = Color.Black;
    public int Radius = 10;

    public StatusPill()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Round.Path(r, Radius))
        using (SolidBrush b = new SolidBrush(Fill))
        {
            e.Graphics.FillPath(b, p);
        }

        TextRenderer.DrawText(
            e.Graphics, Text, Font, r, Ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

internal sealed class RoundButton : Button
{
    public Color NormalBack = Color.Black;
    public Color HoverBack = Color.Black;
    public Color PressBack = Color.Black;
    public Color Ink = Color.White;
    public int Radius = 12;

    private bool hover;
    private bool pressed;

    public RoundButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        TabStop = false;
        DoubleBuffered = true;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hover = false;
        pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);

        Color back = !Enabled ? Color.FromArgb(0xD8, 0xD4, 0xCD)
                   : pressed ? PressBack
                   : hover ? HoverBack
                   : NormalBack;

        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Round.Path(r, Radius))
        using (SolidBrush b = new SolidBrush(back))
        {
            e.Graphics.FillPath(b, p);
        }

        TextRenderer.DrawText(
            e.Graphics, Text, Font, r, Enabled ? Ink : Color.FromArgb(0x9A, 0x95, 0x8E),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
    }
}

#endregion

public class KeyboardLockForm : Form
{
    private const int WH_KEYBOARD_LL = 13;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    // 进程间通信用：第二个实例唤起已有窗口 / 自动化测试用
    private const int WM_APP_SHOW = 0x8000 + 1;
    private const int WM_APP_EXIT = 0x8000 + 2;

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

    private static readonly Color BgColor = Color.FromArgb(0xFA, 0xF9, 0xF7);
    private static readonly Color InkColor = Color.FromArgb(0x1F, 0x23, 0x28);
    private static readonly Color MutedColor = Color.FromArgb(0x8A, 0x85, 0x7E);
    private static readonly Color FaintColor = Color.FromArgb(0xAC, 0xA6, 0x9E);
    private static readonly Color LineColor = Color.FromArgb(0xEC, 0xE7, 0xE0);
    private static readonly Color OkFill = Color.FromArgb(0xEA, 0xF5, 0xEF);
    private static readonly Color OkInk = Color.FromArgb(0x1E, 0x7A, 0x56);
    private static readonly Color LockFill = Color.FromArgb(0xFD, 0xEC, 0xE9);
    private static readonly Color LockInk = Color.FromArgb(0xC0, 0x39, 0x2B);
    private static readonly Color AccentColor = Color.FromArgb(0xE8, 0x71, 0x2B);
    private static readonly Color AccentHover = Color.FromArgb(0xF0, 0x81, 0x3D);
    private static readonly Color AccentPress = Color.FromArgb(0xD4, 0x62, 0x20);
    private static readonly Color InkHover = Color.FromArgb(0x33, 0x38, 0x3F);
    private static readonly Color InkPress = Color.FromArgb(0x0F, 0x12, 0x16);

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private static KeyboardLockForm current;
    private static IntPtr hookId = IntPtr.Zero;
    private static readonly LowLevelKeyboardProc hookProc = HookCallback;

    // 修饰键状态由钩子自己维护：锁定期间按键被吞，操作系统的状态不可信。
    private static bool ctrlDown;
    private static bool altDown;
    private static bool uDown;

    private bool locked;
    private bool hookFailed;
    private bool reallyExit;
    private bool tipShown;
    private float scale = 1F;

    private NotifyIcon tray;
    private ContextMenuStrip trayMenu;
    private ToolStripMenuItem mnuShow;
    private ToolStripMenuItem mnuToggle;

    private StatusPill pill;
    private RoundButton btnToggle;
    private Label lblFoot1;
    private Label lblFoot2;
    private Label lblSub;

    public KeyboardLockForm()
    {
        current = this;

        // 窗口是 DPI 感知的，字体尺寸会自动按 DPI 换算，但硬编码的像素布局不会，
        // 所以这里按当前 DPI 手动缩放所有控件位置。
        AutoScaleMode = AutoScaleMode.None;
        scale = DpiScale();

        Text = "键盘锁";
        ClientSize = new Size(Px(420), Px(296));
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgColor;
        Font = new Font("Microsoft YaHei UI", 9F);

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // 取不到图标不影响功能
        }

        PictureBox pic = new PictureBox();
        pic.SetBounds(Px(28), Px(22), Px(44), Px(44));
        pic.SizeMode = PictureBoxSizeMode.Zoom;
        if (Icon != null)
            pic.Image = new Icon(Icon, new Size(64, 64)).ToBitmap();
        Controls.Add(pic);

        Label lblTitle = new Label();
        lblTitle.SetBounds(Px(84), Px(18), Px(310), Px(28));
        lblTitle.Font = new Font("Microsoft YaHei UI", 15.5F, FontStyle.Bold);
        lblTitle.ForeColor = InkColor;
        lblTitle.Text = "键盘锁";
        Controls.Add(lblTitle);

        lblSub = new Label();
        lblSub.SetBounds(Px(86), Px(48), Px(308), Px(20));
        lblSub.Font = new Font("Microsoft YaHei UI", 9F);
        lblSub.ForeColor = MutedColor;
        lblSub.Text = "只锁键盘 · 鼠标与滚轮始终可用";
        Controls.Add(lblSub);

        Panel line = new Panel();
        line.SetBounds(Px(28), Px(90), Px(364), 1);
        line.BackColor = LineColor;
        Controls.Add(line);

        pill = new StatusPill();
        pill.SetBounds(Px(28), Px(108), Px(364), Px(36));
        pill.Radius = Px(10);
        pill.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
        Controls.Add(pill);

        btnToggle = new RoundButton();
        btnToggle.SetBounds(Px(28), Px(156), Px(364), Px(58));
        btnToggle.Radius = Px(12);
        btnToggle.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
        // 按钮背景必须与窗口底色一致，否则圆角外侧会露出底色（Button 会先铺一层 BackColor）
        btnToggle.BackColor = BgColor;
        btnToggle.FlatAppearance.MouseOverBackColor = BgColor;
        btnToggle.FlatAppearance.MouseDownBackColor = BgColor;
        btnToggle.Click += delegate { Toggle(); };
        Controls.Add(btnToggle);

        lblFoot1 = new Label();
        lblFoot1.SetBounds(Px(28), Px(230), Px(364), Px(18));
        lblFoot1.Font = new Font("Microsoft YaHei UI", 9F);
        lblFoot1.ForeColor = MutedColor;
        lblFoot1.TextAlign = ContentAlignment.MiddleCenter;
        lblFoot1.Text = "Ctrl + Alt + U  随时锁 / 解";
        Controls.Add(lblFoot1);

        lblFoot2 = new Label();
        lblFoot2.SetBounds(Px(28), Px(250), Px(364), Px(18));
        lblFoot2.Font = new Font("Microsoft YaHei UI", 8.5F);
        lblFoot2.ForeColor = FaintColor;
        lblFoot2.TextAlign = ContentAlignment.MiddleCenter;
        lblFoot2.Text = "关闭窗口后仍在托盘运行 · 右键托盘图标可退出";
        Controls.Add(lblFoot2);

        CreateTray();
        UpdateUi();
    }

    private void CreateTray()
    {
        trayMenu = new ContextMenuStrip();
        trayMenu.Font = new Font("Microsoft YaHei UI", 9F);

        mnuShow = new ToolStripMenuItem("显示主窗口");
        mnuShow.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        mnuShow.Click += delegate { ShowFromTray(); };

        mnuToggle = new ToolStripMenuItem("锁定键盘");
        mnuToggle.Click += delegate { Toggle(); };

        ToolStripMenuItem mnuExit = new ToolStripMenuItem("退出");
        mnuExit.Click += delegate { ExitApp(); };

        trayMenu.Items.Add(mnuShow);
        trayMenu.Items.Add(mnuToggle);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(mnuExit);

        tray = new NotifyIcon();
        tray.Icon = Icon;
        tray.Text = "键盘锁";
        tray.ContextMenuStrip = trayMenu;
        tray.Visible = true;
        tray.DoubleClick += delegate { ShowFromTray(); };
        tray.MouseClick += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                ShowFromTray();
        };
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
            pill.Text = "无法安装键盘钩子";
            pill.Fill = LockFill;
            pill.Ink = LockInk;
            lblFoot1.Text = "错误码 " + Marshal.GetLastWin32Error();
            lblFoot2.Text = "请重试，或换一台机器运行";
            MessageBox.Show(
                "键盘钩子安装失败（错误码 " + Marshal.GetLastWin32Error() + "），本程序无法锁定键盘。",
                "键盘锁", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 用户点关闭（X）/ Alt+F4 时只收进托盘；只有托盘菜单「退出」或系统关机才真正结束。
        // 注意不能只判断 CloseReason.UserClosing：外部投递的 WM_CLOSE 拿到的可能是
        // CloseReason.None，那样会绕过收托盘逻辑直接把进程关掉。
        bool systemShutdown =
            e.CloseReason == CloseReason.WindowsShutDown ||
            e.CloseReason == CloseReason.TaskManagerClosing ||
            e.CloseReason == CloseReason.ApplicationExitCall;

        if (!reallyExit && !systemShutdown)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        locked = false;
        if (hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
        }

        if (tray != null)
        {
            tray.Visible = false;
            tray.Dispose();
            tray = null;
        }

        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_APP_SHOW)
        {
            ShowFromTray();
            return;
        }

        if (m.Msg == WM_APP_EXIT)
        {
            ExitApp();
            return;
        }

        base.WndProc(ref m);
    }

    private void HideToTray()
    {
        ShowInTaskbar = false;
        Hide();

        if (!tipShown)
        {
            tipShown = true;
            try
            {
                tray.ShowBalloonTip(
                    4000,
                    "键盘锁仍在后台运行",
                    locked
                        ? "键盘仍处于锁定状态：按 Ctrl + Alt + U，或右键托盘图标解锁。"
                        : "按 Ctrl + Alt + U 可锁定，右键托盘图标可退出。",
                    ToolTipIcon.Info);
            }
            catch
            {
                // 通知中心被关闭时忽略
            }
        }
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void ExitApp()
    {
        reallyExit = true;
        Close();
    }

    private void Toggle()
    {
        if (hookFailed)
            return;

        locked = !locked;
        UpdateUi();

        // 从托盘菜单切换时，如果窗口正好隐藏着，不强行弹出来
        if (!Visible && locked)
        {
            try
            {
                tray.ShowBalloonTip(2500, "键盘已锁定", "按 Ctrl + Alt + U 或右键托盘图标解锁。", ToolTipIcon.Info);
            }
            catch
            {
            }
        }
    }

    private void UpdateUi()
    {
        if (locked)
        {
            pill.Text = "键盘已锁定";
            pill.Fill = LockFill;
            pill.Ink = LockInk;

            btnToggle.Text = "解锁键盘";
            btnToggle.NormalBack = AccentColor;
            btnToggle.HoverBack = AccentHover;
            btnToggle.PressBack = AccentPress;
            btnToggle.Ink = Color.White;

            lblSub.Text = "所有按键已被拦截 · Ctrl + Alt + U 解锁";
            Text = "键盘锁 — 已锁定";

            if (mnuToggle != null)
                mnuToggle.Text = "解锁键盘  (Ctrl+Alt+U)";
            if (tray != null)
                tray.Text = "键盘锁 · 已锁定";
        }
        else
        {
            pill.Text = "键盘未锁定";
            pill.Fill = OkFill;
            pill.Ink = OkInk;

            btnToggle.Text = "锁定键盘";
            btnToggle.NormalBack = InkColor;
            btnToggle.HoverBack = InkHover;
            btnToggle.PressBack = InkPress;
            btnToggle.Ink = Color.White;

            lblSub.Text = "只锁键盘 · 鼠标与滚轮始终可用";
            Text = "键盘锁";

            if (mnuToggle != null)
                mnuToggle.Text = "锁定键盘  (Ctrl+Alt+U)";
            if (tray != null)
                tray.Text = "键盘锁 · 未锁定";
        }

        btnToggle.Invalidate();
        pill.Invalidate();
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

    private int Px(int value)
    {
        return (int)Math.Round(value * scale);
    }

    // 仅在出现未处理异常时写一行日志，便于排查；文件超过 64KB 自动截断。
    private static void Log(string message)
    {
        try
        {
            string path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "keyboardlock-error.log");

            System.IO.FileInfo fi = new System.IO.FileInfo(path);
            if (fi.Exists && fi.Length > 64 * 1024)
                fi.Delete();

            System.IO.File.AppendAllText(
                path,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static float DpiScale()
    {
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
        {
            return g.DpiX / 96F;
        }
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
            // 窗口可能已收进托盘（不可见），FindWindow 仍能找到隐藏窗口
            IntPtr h = FindWindowW(null, "键盘锁 — 已锁定");
            if (h == IntPtr.Zero)
                h = FindWindowW(null, "键盘锁");

            if (h != IntPtr.Zero)
            {
                PostMessageW(h, WM_APP_SHOW, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            int self = Process.GetCurrentProcess().Id;
            foreach (Process p in Process.GetProcessesByName("KeyboardLock"))
            {
                if (p.Id == self)
                    continue;

                IntPtr wnd = p.MainWindowHandle;
                if (wnd == IntPtr.Zero)
                    continue;

                ShowWindow(wnd, SW_RESTORE);
                SetForegroundWindow(wnd);
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

            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs ex)
            {
                Log("ThreadException: " + ex.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs ex)
            {
                Log("UnhandledException: " + ex.ExceptionObject);
            };

            Application.Run(new KeyboardLockForm());
        }
    }
}
