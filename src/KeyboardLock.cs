using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

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

    // 微软官方 WebView2 Runtime（Evergreen）在线安装包。
    private const string WebView2DownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

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
    private bool webReady;
    private bool webRuntimeMissing;

    private WebView2 webView;
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

    private async void OnFormShown(object sender, EventArgs e)
    {
        if (tray == null)
            CreateTray();

        await InitWebViewAsync();

        if (!hookAvailable)
            ShowHookError();
    }

    private static bool IsWebView2RuntimeInstalled()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch
        {
            // WebView2RuntimeNotFoundException 或 Loader 加载失败：都按“不可用”处理。
            return false;
        }
    }

    private void ShowWebView2Missing()
    {
        DialogResult r = MessageBox.Show(
            this,
            "界面需要 Microsoft Edge WebView2 Runtime，本机没有安装。\n" +
            "（Windows 10 / 11 绝大多数已自带，精简版 / LTSC 系统可能没有。）\n\n" +
            "锁定功能不受影响：可以用 Ctrl+Alt+U，或右键托盘图标锁定 / 解锁 / 退出。\n\n" +
            "是否现在打开微软官方下载？安装后重新启动本程序即可。",
            "键盘锁",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (r == DialogResult.Yes)
        {
            try
            {
                Process.Start(WebView2DownloadUrl);
            }
            catch
            {
            }
        }
    }

    private async Task InitWebViewAsync()
    {
        if (!IsWebView2RuntimeInstalled())
        {
            // 没有运行时就没有界面：收进托盘，只保留快捷键和托盘菜单，不留一块空白窗口。
            webRuntimeMissing = true;
            ShowWebView2Missing();
            Hide();
            return;
        }

        try
        {
            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            // 与窗体同色的不透明背景：免去透明合成，加载前也不会闪白。
            webView.DefaultBackgroundColor = BackColor;
            webView.AllowExternalDrop = false;
            Controls.Add(webView);

            string userDataDir = Path.Combine(Program.AppDataDir, "webview");
            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
            await webView.EnsureCoreWebView2Async(env);

            CoreWebView2 core = webView.CoreWebView2;
            CoreWebView2Settings s = core.Settings;
            s.AreDefaultContextMenusEnabled = false;
            s.AreDevToolsEnabled = false;
            s.IsStatusBarEnabled = false;
            s.IsZoomControlEnabled = false;
            s.AreBrowserAcceleratorKeysEnabled = false; // F5 / Ctrl+P / Ctrl+F 等浏览器快捷键对这个面板没有意义

            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationCompleted += OnNavigationCompleted;
            core.ProcessFailed += OnWebProcessFailed;

            core.NavigateToString(LoadUiHtml());
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "界面初始化失败：" + ex.Message, "键盘锁", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        webReady = true;
        SyncStateToWeb();
    }

    private void OnWebProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
    {
        // 渲染进程崩溃时页面会停在旧状态，界面可能显示“未锁定”而键盘其实锁着——必须重建并重新同步。
        webReady = false;

        if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
        {
            if (!reallyExit && !IsDisposed)
                BeginInvoke((MethodInvoker)RecreateWebView);
        }
        else if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited
            || e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
        {
            try
            {
                webView.CoreWebView2.NavigateToString(LoadUiHtml()); // NavigationCompleted 会重新同步状态
            }
            catch
            {
            }
        }
    }

    private async void RecreateWebView()
    {
        if (webView != null)
        {
            Controls.Remove(webView);
            webView.Dispose();
            webView = null;
        }

        if (!reallyExit && !IsDisposed)
            await InitWebViewAsync();
    }

    private static string LoadUiHtml()
    {
        // 只使用编译时嵌入的界面，避免 exe 旁边残留的旧 ui.html 覆盖新版本。
        // 调试界面时可以直接用浏览器打开 src/ui.html（脚本自带离线回退）。
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
        string command;
        try
        {
            command = e.TryGetWebMessageAsString();
        }
        catch (ArgumentException)
        {
            return; // 页面只发送纯字符串命令，其余一律忽略
        }

        switch (command)
        {
            case "toggle":
                ToggleLock();
                break;
            case "hide":
                Hide();
                break;
            case "minimize":
                WindowState = FormWindowState.Minimized;
                break;
            case "drag":
                // 交给系统的原生移动循环：SendMessage 会一直阻塞到松开鼠标（或按 ESC 取消）。
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                break;
        }
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

        SyncStateToWeb();
    }

    private void SyncStateToWeb()
    {
        // 窗口隐藏时页面处于挂起状态，不去唤醒它；重新显示时会再同步一次。
        if (!webReady || !Visible || webView == null || webView.CoreWebView2 == null)
            return;

        try
        {
            webView.CoreWebView2.PostWebMessageAsJson(
                "{\"locked\":" + (KeyboardHook.IsLocked ? "true" : "false") +
                ",\"available\":" + (hookAvailable ? "true" : "false") + "}");
        }
        catch
        {
            webReady = false; // 通道已失效，等 ProcessFailed 重建后由 NavigationCompleted 恢复
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateWebViewActivity();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateWebViewActivity();
    }

    /// <summary>
    /// 收进托盘：挂起 WebView2 渲染进程（停止一切调度并释放内存）；
    /// 最小化：请求低内存模式；重新显示：恢复并同步状态。键盘钩子在独立线程上，完全不受影响。
    /// </summary>
    private async void UpdateWebViewActivity()
    {
        if (webView == null || webView.CoreWebView2 == null)
            return;

        CoreWebView2 core = webView.CoreWebView2;
        try
        {
            if (!Visible)
            {
                // 窗体隐藏后 WebView2 控件已自动置 IsVisible = false，满足挂起条件。
                if (!core.IsSuspended)
                    await core.TrySuspendAsync();
            }
            else
            {
                if (core.IsSuspended)
                    core.Resume();

                core.MemoryUsageTargetLevel = WindowState == FormWindowState.Minimized
                    ? CoreWebView2MemoryUsageTargetLevel.Low
                    : CoreWebView2MemoryUsageTargetLevel.Normal;

                SyncStateToWeb();
            }
        }
        catch
        {
            // 挂起 / 低内存只是优化，失败不影响功能。
        }
    }

    private void ShowFromTray()
    {
        if (webRuntimeMissing)
        {
            ShowWebView2Missing();
            return;
        }

        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
        SyncStateToWeb();
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

        // webView 是子控件，随窗体一起释放；以下几个不在控件树里，需要手动释放。
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

    internal static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyboardLock");

    private static void ActivateExistingInstance()
    {
        if (ShowInstanceMessage == 0)
            return;

        // 当前进程是用户刚双击启动的，持有前台权限；转交给第一个实例，它才能把窗口切到前台。
        AllowSetForegroundWindow(ASFW_ANY);
        PostMessageW(HWND_BROADCAST, ShowInstanceMessage, IntPtr.Zero, IntPtr.Zero);
    }

    private static readonly string[] EmbeddedDlls = new string[]
    {
        "WebView2Loader.dll",
        "Microsoft.Web.WebView2.Core.dll",
        "Microsoft.Web.WebView2.WinForms.dll"
    };

    private static readonly Dictionary<string, Assembly> resolvedAssemblies =
        new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 释放 WebView2 运行库所在的目录：%LocalAppData%\KeyboardLock\bin\&lt;构建标识&gt;。
    /// 释放失败时为 null（此时托管程序集直接从内存加载，原生 Loader 退回到系统默认搜索路径）。
    /// </summary>
    internal static string NativeBinDir { get; private set; }

    private static byte[] ReadEmbedded(Assembly asm, string fileName)
    {
        using (Stream s = asm.GetManifestResourceStream("KeyboardLock." + fileName))
        {
            if (s == null)
                return null;
            using (MemoryStream ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }

    private static bool HasLength(string path, long length)
    {
        FileInfo fi = new FileInfo(path);
        return fi.Exists && fi.Length == length;
    }

    private static void WriteAtomically(Stream source, string target)
    {
        // 先写临时文件再改名，避免中途断电/被杀留下半截 DLL。
        string tmp = target + "." + Process.GetCurrentProcess().Id + ".tmp";
        source.Position = 0;
        using (FileStream fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            source.CopyTo(fs);
        if (File.Exists(target))
            File.Delete(target);
        File.Move(tmp, target);
    }

    /// <summary>
    /// 把 exe 内嵌的 3 个 WebView2 DLL 释放到用户可写的 %LocalAppData%：
    /// 不依赖 exe 所在目录可写（Program Files、只读 U 盘也能用）。
    /// 目录名取本 exe 的模块版本 ID（每次编译都不同），升级后自动换新目录、清掉旧目录；
    /// 已释放过时只比较文件长度，既不哈希、也不把内嵌资源读进内存。
    /// </summary>
    private static void PrepareEmbeddedBinaries()
    {
        try
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string tag = asm.ManifestModule.ModuleVersionId.ToString("N").Substring(0, 12);
            string binRoot = Path.Combine(AppDataDir, "bin");
            string dir = Path.Combine(binRoot, tag);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            for (int i = 0; i < EmbeddedDlls.Length; i++)
            {
                using (Stream s = asm.GetManifestResourceStream("KeyboardLock." + EmbeddedDlls[i]))
                {
                    if (s == null)
                        return; // 不是完整的单文件构建，交给系统默认搜索路径

                    if (i == 0)
                        Directory.CreateDirectory(dir);

                    string target = Path.Combine(dir, EmbeddedDlls[i]);
                    if (!HasLength(target, s.Length))
                        WriteAtomically(s, target);

                    // 旧版本留在 exe 旁边的散装同名 DLL 会被 CLR 优先加载；大小不一致时顺手更新。
                    try
                    {
                        string local = Path.Combine(baseDir, EmbeddedDlls[i]);
                        if (File.Exists(local) && !HasLength(local, s.Length))
                            WriteAtomically(s, local);
                    }
                    catch
                    {
                    }
                }
            }

            NativeBinDir = dir;
            CleanupOldBinDirs(binRoot, dir);
        }
        catch
        {
            NativeBinDir = null;
        }
    }

    private static void CleanupOldBinDirs(string binRoot, string keep)
    {
        try
        {
            foreach (string d in Directory.GetDirectories(binRoot))
            {
                if (string.Equals(d, keep, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    Directory.Delete(d, true);
                }
                catch
                {
                    // 可能正被别的进程占用，下次启动再清。
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
            string fileName = shortName + ".dll";
            if (Array.IndexOf(EmbeddedDlls, fileName) < 0)
                return null;

            lock (resolvedAssemblies)
            {
                // 同一个程序集可能被多次请求（主程序、WinForms 包各一次），必须返回同一个实例，
                // 否则内存加载会出现两份 Core，类型互不兼容。
                Assembly cached;
                if (resolvedAssemblies.TryGetValue(shortName, out cached))
                    return cached;

                Assembly loaded = null;
                if (NativeBinDir != null)
                {
                    string path = Path.Combine(NativeBinDir, fileName);
                    if (File.Exists(path))
                        loaded = Assembly.LoadFrom(path);
                }

                if (loaded == null)
                {
                    byte[] raw = ReadEmbedded(Assembly.GetExecutingAssembly(), fileName);
                    if (raw != null)
                        loaded = Assembly.Load(raw);
                }

                if (loaded != null)
                    resolvedAssemblies[shortName] = loaded;
                return loaded;
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
        if (NativeBinDir != null)
        {
            try
            {
                CoreWebView2Environment.SetLoaderDllFolderPath(NativeBinDir);
            }
            catch
            {
            }
        }

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

            PrepareEmbeddedBinaries();
            RunApp();
        }
    }
}
