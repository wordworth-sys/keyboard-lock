using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using KeyboardLock.Alu;

public class KeyboardLockForm : Form
{
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    private const int WM_APP_SHOW = 0x8000 + 1;
    private const int WM_APP_EXIT = 0x8000 + 2;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowTextW(IntPtr hWnd, string lpString);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private bool hookAvailable;
    private int hookError;
    private bool reallyExit;

    private AluPanelView panelView;
    private NotifyIcon tray;
    private ContextMenuStrip trayMenu;
    private ToolStripMenuItem mnuToggle;
    private Font menuFont;
    private Font menuFontBold;

    public KeyboardLockForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = "键盘锁";
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(0xD3, 0xD9, 0xE2);

        float scale = GetSystemDpiScale();
        int w = (int)Math.Round(400 * scale);
        int h = (int)Math.Round(264 * scale);
        Width = w;
        Height = h;
        ClientSize = new Size(w, h);

        // 图标和圆角区域只创建一次：窗口句柄重建时 WinForms 会沿用这两个属性，不会重复分配 GDI 对象。
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
        }

        int radius = (int)Math.Round(16 * scale);
        IntPtr hrgn = CreateRoundRectRgn(0, 0, w + 1, h + 1, radius * 2, radius * 2);
        if (hrgn != IntPtr.Zero)
        {
            Region = Region.FromHrgn(hrgn);
            DeleteObject(hrgn);
        }

        KeyboardHook.HotkeyToggled += OnHotkeyToggled;
        Shown += OnFormShown;

        panelView = new AluPanelView();
        panelView.Dock = DockStyle.Fill;
        panelView.ToggleRequested += delegate { ToggleLock(); };
        panelView.MinimizeRequested += delegate { WindowState = FormWindowState.Minimized; };
        panelView.CloseToTrayRequested += delegate { Hide(); };
        panelView.DragRequested += delegate { StartWindowDrag(); };
        Controls.Add(panelView);
    }

    /// <summary>用系统原生移动循环拖动窗口（按住 ESC 可取消）。</summary>
    private void StartWindowDrag()
    {
        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
    }

    private static float GetSystemDpiScale()
    {
        try
        {
            uint dpi = GetDpiForSystem();
            if (dpi > 0)
                return dpi / 96f;
        }
        catch
        {
        }
        return 1f;
    }

    private void CreateTray()
    {
        menuFont = new Font("Microsoft YaHei UI", 9F);
        menuFontBold = new Font(menuFont, FontStyle.Bold);

        trayMenu = new ContextMenuStrip();
        trayMenu.Font = menuFont;

        ToolStripMenuItem mnuShow = new ToolStripMenuItem("显示主界面");
        mnuShow.Font = menuFontBold;
        mnuShow.Click += delegate { ShowFromTray(); };

        mnuToggle = new ToolStripMenuItem("锁定键盘 (Ctrl+Alt+U)");
        mnuToggle.Enabled = hookAvailable;
        mnuToggle.Click += delegate { ToggleLock(); };

        ToolStripMenuItem mnuExit = new ToolStripMenuItem("完全退出");
        mnuExit.Click += delegate { ExitApp(); };

        trayMenu.Items.Add(mnuShow);
        trayMenu.Items.Add(mnuToggle);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(mnuExit);

        tray = new NotifyIcon();
        tray.Icon = Icon;
        tray.Text = hookAvailable ? "键盘锁 · 未锁定" : "键盘锁 · 钩子不可用";
        tray.ContextMenuStrip = trayMenu;
        tray.Visible = true;
        tray.DoubleClick += delegate { ShowFromTray(); };
        tray.MouseClick += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                ShowFromTray();
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.Parent = IntPtr.Zero;
            cp.ExStyle |= 0x00040000; // WS_EX_APPWINDOW
            // 无边框窗体默认没有这两个样式：补上后，界面里的「–」有系统最小化动画，
            // 点任务栏按钮也能正常最小化/还原；无标题栏时它们不会绘制任何系统按钮。
            cp.Style |= 0x00020000 | 0x00080000; // WS_MINIMIZEBOX | WS_SYSMENU
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        SetWindowTextW(Handle, Text);

        try
        {
            int preference = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
        }
        catch
        {
        }

        StartHook();
    }

    private void StartHook()
    {
        int error;
        hookAvailable = KeyboardHook.Start(out error);
        hookError = error;
    }

    private void OnHotkeyToggled()
    {
        // 在钩子线程上被调用：只投递，绝不在这里做任何耗时操作。
        try
        {
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke((MethodInvoker)OnLockStateChanged);
        }
        catch (InvalidOperationException)
        {
            // 窗口句柄恰好在退出过程中被销毁，忽略即可。
        }
    }

    private void ShowHookError()
    {
        MessageBox.Show(
            this,
            "键盘钩子安装失败（Win32 错误码 " + hookError + "），锁定功能已禁用。\n\n" +
            "常见原因：杀毒软件 / 安全软件拦截了全局键盘钩子。\n" +
            "可以把 KeyboardLock.exe 加入信任列表后重新启动程序。",
            "键盘锁",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void OnFormShown(object sender, EventArgs e)
    {
        if (tray == null)
            CreateTray();

        UpdatePanelState();

        if (!hookAvailable)
            ShowHookError();
    }

    private void ToggleLock()
    {
        // 钩子没装上时拒绝切换，否则界面会显示"已锁定"而键盘实际仍可用。
        if (!hookAvailable)
            return;

        KeyboardHook.Toggle();
        OnLockStateChanged();
    }

    private void OnLockStateChanged()
    {
        bool isLocked = KeyboardHook.IsLocked;

        Text = isLocked ? "键盘锁 — 已锁定" : "键盘锁";
        if (IsHandleCreated)
            SetWindowTextW(Handle, Text);

        if (mnuToggle != null)
            mnuToggle.Text = isLocked ? "解锁键盘 (Ctrl+Alt+U)" : "锁定键盘 (Ctrl+Alt+U)";
        if (tray != null)
            tray.Text = isLocked ? "键盘锁 · 已锁定" : "键盘锁 · 未锁定";

        if (IsHandleCreated)
        {
            SetWindowPos(
                Handle,
                isLocked ? HWND_TOPMOST : HWND_NOTOPMOST,
                0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        UpdatePanelState();
    }

    /// <summary>把当前锁定状态同步到自绘面板。</summary>
    private void UpdatePanelState()
    {
        if (panelView == null)
            return;

        if (!hookAvailable)
            panelView.State = AluPanelState.Unavailable;
        else
            panelView.State = KeyboardHook.IsLocked ? AluPanelState.Locked : AluPanelState.Unlocked;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
        UpdatePanelState();
    }

    private void ExitApp()
    {
        reallyExit = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        bool systemShutdown =
            e.CloseReason == CloseReason.WindowsShutDown ||
            e.CloseReason == CloseReason.TaskManagerClosing ||
            e.CloseReason == CloseReason.ApplicationExitCall;

        if (!reallyExit && !systemShutdown)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        reallyExit = true;
        KeyboardHook.HotkeyToggled -= OnHotkeyToggled;
        KeyboardHook.Stop(); // 同时会解除锁定

        // 托盘图标与菜单不在控件树里，需要手动释放。
        if (tray != null)
        {
            tray.Visible = false;
            tray.Dispose();
            tray = null;
        }
        if (trayMenu != null)
        {
            trayMenu.Dispose();
            trayMenu = null;
        }
        if (menuFontBold != null)
        {
            menuFontBold.Dispose();
            menuFontBold = null;
        }
        if (menuFont != null)
        {
            menuFont.Dispose();
            menuFont = null;
        }

        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if ((m.Msg == Program.ShowInstanceMessage && m.Msg != 0) || m.Msg == WM_APP_SHOW)
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
}

internal static class Program
{
    private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);
    private const int ASFW_ANY = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string lpString);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>
    /// 第二个实例用来唤醒第一个实例的系统级唯一消息。广播给所有顶层窗口（包括隐藏在托盘里的），
    /// 不依赖窗口标题——同名的资源管理器文件夹窗口不会被误匹配——也不依赖 MainWindowHandle。
    /// </summary>
    internal static readonly int ShowInstanceMessage = (int)RegisterWindowMessageW("KeyboardLock.ShowInstance.{6F1C2A4E}");

    private static void ActivateExistingInstance()
    {
        if (ShowInstanceMessage == 0)
            return;

        // 当前进程是用户刚双击启动的，持有前台权限；转交给第一个实例，它才能把窗口切到前台。
        AllowSetForegroundWindow(ASFW_ANY);
        PostMessageW(HWND_BROADCAST, ShowInstanceMessage, IntPtr.Zero, IntPtr.Zero);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunApp()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        KeyboardLockForm form = new KeyboardLockForm();
        form.Show();
        ShowWindow(form.Handle, 5); // SW_SHOW: 防止从后台终端启动时受 STARTUPINFO SW_HIDE 影响
        Application.Run(form);
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

            RunApp();
        }
    }
}
