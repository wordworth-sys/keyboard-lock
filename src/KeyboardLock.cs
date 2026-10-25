using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

public class KeyboardLockForm : Form
{
    private const int WH_KEYBOARD_LL = 13;

    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x2;

    private const int WM_APP_SHOW = 0x8000 + 1;
    private const int WM_APP_EXIT = 0x8000 + 2;

    private const int VK_LBUTTON = 0x01;
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
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowTextW(IntPtr hWnd, string lpString);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private static KeyboardLockForm current;
    private static IntPtr hookId = IntPtr.Zero;
    private static readonly LowLevelKeyboardProc hookProc = HookCallback;

    private static bool ctrlDown;
    private static bool altDown;
    private static bool uDown;

    private volatile bool isLocked;
    private bool reallyExit;
    private bool webReady;

    private WebView2 webView;
    private NotifyIcon tray;
    private ContextMenuStrip trayMenu;
    private ToolStripMenuItem mnuToggle;

    private System.Windows.Forms.Timer dragTimer;
    private Point dragStartCursor;
    private Point dragStartLocation;

    public KeyboardLockForm()
    {
        current = this;

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

        dragTimer = new System.Windows.Forms.Timer();
        dragTimer.Interval = 8;
        dragTimer.Tick += OnDragTick;

        Shown += OnFormShown;
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
        trayMenu = new ContextMenuStrip();
        trayMenu.Font = new Font("Microsoft YaHei UI", 9F);

        ToolStripMenuItem mnuShow = new ToolStripMenuItem("显示主界面");
        mnuShow.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
        mnuShow.Click += delegate { ShowFromTray(); };

        mnuToggle = new ToolStripMenuItem("锁定键盘 (Ctrl+Alt+U)");
        mnuToggle.Click += delegate { ToggleLock(); };

        ToolStripMenuItem mnuExit = new ToolStripMenuItem("完全退出");
        mnuExit.Click += delegate { ExitApp(); };

        trayMenu.Items.Add(mnuShow);
        trayMenu.Items.Add(mnuToggle);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(mnuExit);

        tray = new NotifyIcon();
        tray.Icon = Icon;
        tray.Text = "键盘锁 · 未锁定";
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
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        SetWindowTextW(Handle, Text);

        float scale = GetSystemDpiScale();
        int radius = (int)Math.Round(16 * scale);
        Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, radius * 2, radius * 2));

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
        }

        try
        {
            int preference = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
        }
        catch
        {
        }

        InstallHook();
    }

    private async void OnFormShown(object sender, EventArgs e)
    {
        if (tray == null)
        {
            CreateTray();
        }

        try
        {
            if (webView == null)
            {
                webView = new WebView2();
                webView.Dock = DockStyle.Fill;
                webView.DefaultBackgroundColor = Color.Transparent;
                webView.AllowExternalDrop = false;
                Controls.Add(webView);
            }

            var localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyboardLock");
            Directory.CreateDirectory(localData);
            var env = await CoreWebView2Environment.CreateAsync(null, localData);
            await webView.EnsureCoreWebView2Async(env);
            webView.DefaultBackgroundColor = Color.Transparent;

            CoreWebView2Settings s = webView.CoreWebView2.Settings;
            s.AreDefaultContextMenusEnabled = false;
            s.AreDevToolsEnabled = false;
            s.IsStatusBarEnabled = false;
            s.IsZoomControlEnabled = false;

            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            webView.CoreWebView2.NavigationCompleted += delegate
            {
                webReady = true;
                SyncStateToWeb();
            };

            webView.CoreWebView2.NavigateToString(LoadUiHtml());
        }
        catch (Exception ex)
        {
            MessageBox.Show("WebView2 初始化失败：" + ex.Message, "键盘锁", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string LoadUiHtml()
    {
        string localFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui.html");
        if (File.Exists(localFile))
        {
            return File.ReadAllText(localFile, Encoding.UTF8);
        }

        Assembly asm = Assembly.GetExecutingAssembly();
        using (Stream s = asm.GetManifestResourceStream("KeyboardLock.ui.html"))
        {
            if (s != null)
            {
                using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                    return r.ReadToEnd();
            }
        }
        return "<html><body>UI missing</body></html>";
    }

    private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string json = e.WebMessageAsJson;
        if (string.IsNullOrEmpty(json))
            return;

        if (json.IndexOf("\"toggle\"", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            ToggleLock();
        }
        else if (json.IndexOf("\"hide\"", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            this.Hide();
        }
        else if (json.IndexOf("\"minimize\"", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            this.WindowState = FormWindowState.Minimized;
        }
        else if (json.IndexOf("\"drag\"", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            StartNativeDrag();
        }
    }

    private void StartNativeDrag()
    {
        dragStartCursor = Cursor.Position;
        dragStartLocation = this.Location;

        ReleaseCapture();
        SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);

        if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0)
        {
            dragTimer.Start();
        }
    }

    private void OnDragTick(object sender, EventArgs e)
    {
        if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0)
        {
            dragTimer.Stop();
            return;
        }

        Point cur = Cursor.Position;
        int nx = dragStartLocation.X + (cur.X - dragStartCursor.X);
        int ny = dragStartLocation.Y + (cur.Y - dragStartCursor.Y);
        if (Location.X != nx || Location.Y != ny)
        {
            Location = new Point(nx, ny);
        }
    }

    private void InstallHook()
    {
        if (hookId != IntPtr.Zero)
            return;

        IntPtr hMod = GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName);
        hookId = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, hMod, 0);
    }

    private void UninstallHook()
    {
        if (hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
        }
    }

    private void ToggleLock()
    {
        isLocked = !isLocked;
        OnLockStateChanged();
    }

    private void OnLockStateChanged()
    {
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

        SyncStateToWeb();
    }

    private void SyncStateToWeb()
    {
        if (!webReady || webView == null || webView.CoreWebView2 == null)
            return;

        try
        {
            webView.CoreWebView2.PostWebMessageAsJson("{\"locked\":" + (isLocked ? "true" : "false") + "}");
        }
        catch
        {
        }
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.Activate();
        this.BringToFront();
        SyncStateToWeb();
    }

    private void ExitApp()
    {
        reallyExit = true;
        this.Close();
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
            this.Hide();
            return;
        }

        isLocked = false;
        dragTimer.Stop();
        UninstallHook();

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
                        current.isLocked = !current.isLocked;
                        if (current.IsHandleCreated)
                        {
                            current.BeginInvoke((MethodInvoker)delegate
                            {
                                current.OnLockStateChanged();
                            });
                        }
                        return (IntPtr)1;
                    }
                }
            }

            if (IsModifier(vk))
            {
                if (isUp)
                    return CallNextHookEx(hookId, nCode, wParam, lParam);
                return current.isLocked ? (IntPtr)1 : CallNextHookEx(hookId, nCode, wParam, lParam);
            }

            if (current.isLocked)
                return (IntPtr)1;

            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }
        catch
        {
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }
    }
}

internal static class Program
{
    private const int SW_RESTORE = 9;
    private const int WM_APP_SHOW = 0x8000 + 1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private static void ActivateExistingInstance()
    {
        try
        {
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
        }
    }

    private static void EnsurePhysicalDllsExtracted()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] dlls = new string[]
            {
                "WebView2Loader.dll",
                "Microsoft.Web.WebView2.Core.dll",
                "Microsoft.Web.WebView2.WinForms.dll"
            };
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string dll in dlls)
            {
                string target = Path.Combine(baseDir, dll);
                if (File.Exists(target))
                    continue;

                using (Stream s = asm.GetManifestResourceStream("KeyboardLock." + dll))
                {
                    if (s != null)
                    {
                        using (FileStream fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            s.CopyTo(fs);
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
    {
        try
        {
            string shortName = new AssemblyName(args.Name).Name;
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, shortName + ".dll");
            if (File.Exists(localPath))
                return Assembly.LoadFrom(localPath);

            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream("KeyboardLock." + shortName + ".dll"))
            {
                if (s == null)
                    return null;

                byte[] raw = new byte[s.Length];
                int read = 0;
                while (read < raw.Length)
                {
                    int n = s.Read(raw, read, raw.Length - read);
                    if (n <= 0)
                        break;
                    read += n;
                }
                return Assembly.Load(raw);
            }
        }
        catch
        {
            return null;
        }
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
        AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;

        bool createdNew;
        using (Mutex mutex = new Mutex(true, @"Local\KeyboardLock_SingleInstance", out createdNew))
        {
            if (!createdNew)
            {
                ActivateExistingInstance();
                return;
            }

            EnsurePhysicalDllsExtracted();
            RunApp();
        }
    }
}
