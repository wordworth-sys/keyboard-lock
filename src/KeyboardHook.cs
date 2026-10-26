using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// 全局低层键盘钩子（WH_KEYBOARD_LL），运行在独立的专用线程上。
///
/// 为什么不放在 UI 线程：系统要求钩子回调在 LowLevelHooksTimeout（约 1 秒）内返回，
/// 超时多次后 Win7+ 会静默摘除钩子且不通知程序——界面会继续显示"已锁定"，键盘却已恢复。
/// UI 线程一旦卡顿（弹窗、WebView2 初始化、GC），还会拖慢全系统的每一次按键。
/// 专用线程只跑一个空消息循环，回调永远能立即得到处理。
/// </summary>
internal static class KeyboardHook
{
    private const int WH_KEYBOARD_LL = 13;
    private const int HC_ACTION = 0;
    private const int LLKHF_UP = 0x80;

    private const uint WM_QUIT = 0x0012;
    private const uint PM_NOREMOVE = 0x0000;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    // 必须用静态字段持有委托，防止被 GC 回收后钩子回调到野指针。
    private static readonly LowLevelKeyboardProc hookProc = HookCallback;
    private static readonly object startStopLock = new object();

    private static Thread thread;
    private static uint threadId;
    private static IntPtr hookId = IntPtr.Zero;

    // 0 = 未锁定，1 = 已锁定。UI 线程和钩子线程都会读写，统一走 Interlocked/Volatile。
    private static int lockedFlag;

    // 只在钩子线程上访问。
    private static readonly KeyFilter filter = new KeyFilter();

    /// <summary>用户按下 Ctrl+Alt+U 切换了锁定状态。注意：在钩子线程上触发，订阅方需自行封送回 UI 线程。</summary>
    public static event Action HotkeyToggled;

    public static bool IsLocked
    {
        get { return Volatile.Read(ref lockedFlag) != 0; }
    }

    public static bool IsRunning
    {
        get { return Volatile.Read(ref hookId) != IntPtr.Zero; }
    }

    public static void SetLocked(bool locked)
    {
        Interlocked.Exchange(ref lockedFlag, locked ? 1 : 0);
    }

    /// <summary>原子地翻转锁定状态，返回翻转后的值。</summary>
    public static bool Toggle()
    {
        int oldValue, newValue;
        do
        {
            oldValue = Volatile.Read(ref lockedFlag);
            newValue = oldValue ^ 1;
        }
        while (Interlocked.CompareExchange(ref lockedFlag, newValue, oldValue) != oldValue);
        return newValue != 0;
    }

    /// <summary>启动钩子线程并安装钩子。安装失败时返回 false，并给出 Win32 错误码。</summary>
    public static bool Start(out int win32Error)
    {
        lock (startStopLock)
        {
            win32Error = 0;
            if (thread != null)
                return IsRunning;

            bool ok = false;
            int error = 0;

            using (ManualResetEvent ready = new ManualResetEvent(false))
            {
                Thread t = new Thread(delegate()
                {
                    MSG msg;
                    threadId = GetCurrentThreadId();
                    // 先让系统为本线程建立消息队列，保证之后 PostThreadMessage(WM_QUIT) 一定能送达。
                    PeekMessage(out msg, IntPtr.Zero, 0, 0, PM_NOREMOVE);

                    IntPtr h = SetWindowsHookEx(WH_KEYBOARD_LL, hookProc, GetModuleHandle(null), 0);
                    if (h == IntPtr.Zero)
                        error = Marshal.GetLastWin32Error();
                    else
                        ok = true;

                    Volatile.Write(ref hookId, h);
                    ready.Set();

                    if (!ok)
                        return;

                    // 低层钩子回调在本线程从 GetMessage 中取消息时被调用；收到 WM_QUIT 时 GetMessage 返回 0。
                    while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
                    {
                    }

                    UnhookWindowsHookEx(h);
                    Volatile.Write(ref hookId, IntPtr.Zero);
                });
                t.Name = "KeyboardLock.Hook";
                t.IsBackground = true;
                t.Priority = ThreadPriority.Highest;
                t.Start();

                ready.WaitOne();
                thread = ok ? t : null;
            }

            win32Error = error;
            return ok;
        }
    }

    /// <summary>解除锁定、卸载钩子并结束钩子线程。</summary>
    public static void Stop()
    {
        lock (startStopLock)
        {
            SetLocked(false);
            if (thread == null)
                return;

            PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread.Join(1000);
            thread = null;
        }
    }

    private static void RaiseHotkeyToggled()
    {
        Action handler = HotkeyToggled;
        if (handler == null)
            return;
        try
        {
            handler();
        }
        catch
        {
        }
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != HC_ACTION)
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);

        // KBDLLHOOKSTRUCT: vkCode @0, scanCode @4, flags @8, time @12。逐字段读取，不分配。
        int action;
        try
        {
            int vk = Marshal.ReadInt32(lParam);
            int scanCode = Marshal.ReadInt32(lParam, 4);
            int flags = Marshal.ReadInt32(lParam, 8);
            uint time = unchecked((uint)Marshal.ReadInt32(lParam, 12));
            action = filter.Process(vk, scanCode, (flags & LLKHF_UP) != 0, time, IsLocked);
        }
        catch
        {
            // 异常绝不能穿出回调（会终止进程）；出错时宁可放行这一个键。
            action = KeyFilter.Pass;
        }

        if (action == KeyFilter.ToggleLock)
        {
            Toggle();
            RaiseHotkeyToggled();
            return (IntPtr)1;
        }

        return action == KeyFilter.Block
            ? (IntPtr)1
            : CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }
}
