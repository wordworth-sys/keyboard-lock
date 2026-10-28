using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

/// <summary>
/// 开机静默启动：往「启动」文件夹放一个带 <c>--silent</c> 参数的快捷方式。
///
/// 为什么用启动文件夹而不是注册表：保持「不写注册表」这一条产品承诺，
/// 用户随时可以自己删掉快捷方式，或在任务管理器「启动」标签页里一键禁用。
///
/// 快捷方式指向「当前这个 exe」：程序每次启动都会自愈一次，
/// 因此把 exe 挪到别的目录、或换了新版本后，自启项仍会自动指向新的位置。
/// </summary>
internal static class AutoStart
{
    /// <summary>静默启动参数：带上它就只进托盘、不显示窗口。</summary>
    public const string SilentArgument = "--silent";

    private const string ShortcutName = "键盘锁.lnk";
    private const string Description = "键盘锁 · 开机静默启动（静默驻留托盘）";

    /// <summary>启动文件夹里快捷方式的完整路径。</summary>
    public static string ShortcutPath
    {
        get
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return string.IsNullOrEmpty(folder) ? null : Path.Combine(folder, ShortcutName);
        }
    }

    /// <summary>当前是否已注册开机自启（以快捷方式是否存在为准）。</summary>
    public static bool IsEnabled
    {
        get
        {
            string path = ShortcutPath;
            if (string.IsNullOrEmpty(path))
                return false;
            try
            {
                return File.Exists(path);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>注册开机自启（指向当前运行的 exe）。</summary>
    public static bool Enable()
    {
        return Enable(CurrentExecutablePath());
    }

    /// <summary>注册开机自启（指向指定的 exe）。</summary>
    public static bool Enable(string exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return false;

        string path = ShortcutPath;
        if (string.IsNullOrEmpty(path))
            return false;

        try
        {
            string dir = Path.GetDirectoryName(exePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                return false;

            return WriteShortcut(path, exePath, dir);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>取消开机自启。</summary>
    public static bool Disable()
    {
        string path = ShortcutPath;
        if (string.IsNullOrEmpty(path))
            return false;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 自愈：快捷方式还在、但指向的不是当前 exe（挪了目录 / 换了版本）时重建它。
    /// 在后台 STA 线程上执行，不挡在窗口显示前面；程序启动时调用一次即可。
    /// WScript.Shell 是单线程套间（STA）COM 组件，所以不走线程池（MTA），而是起一个专用 STA 线程。
    /// 未开启自启时只做一次 File.Exists 就返回，开销可忽略。
    /// </summary>
    public static void EnsureCurrentAsync()
    {
        // exe 路径在调用线程上取好：Application.ExecutablePath 依赖主线程上下文。
        string exePath = CurrentExecutablePath();
        if (string.IsNullOrEmpty(exePath) || !IsEnabled)
            return;

        Thread t = new Thread(delegate()
        {
            try
            {
                EnsureCurrent(exePath);
            }
            catch
            {
                // 自愈失败不影响主流程：下次启动会再试一次。
            }
        });
        t.Name = "KeyboardLock.AutoStartHeal";
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }

    /// <summary>
    /// 自愈：快捷方式还在、但指向的不是指定 exe 时重建它。
    /// </summary>
    public static void EnsureCurrent(string exePath)
    {
        if (!IsEnabled || string.IsNullOrEmpty(exePath))
            return;

        string target = ReadTarget();
        if (!string.IsNullOrEmpty(target) && PathsEqual(target, exePath))
            return;

        Enable(exePath);
    }

    private static string CurrentExecutablePath()
    {
        try
        {
            return Application.ExecutablePath;
        }
        catch
        {
            return null;
        }
    }

    private static bool PathsEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(a).TrimEnd('\\'),
                Path.GetFullPath(b).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private delegate T ShortcutOperation<T>(Type shortcutType, object shortcut);

    /// <summary>用 WSH（系统自带、无需额外依赖）写 .lnk，避免引入 COM 互操作样板代码。</summary>
    private static bool WriteShortcut(string shortcutPath, string targetPath, string workingDir)
    {
        return WithShortcut(shortcutPath, delegate(Type sc, object shortcut)
        {
            Set(sc, shortcut, "TargetPath", targetPath);
            Set(sc, shortcut, "Arguments", SilentArgument);
            Set(sc, shortcut, "WorkingDirectory", workingDir);
            Set(sc, shortcut, "IconLocation", targetPath + ",0");
            Set(sc, shortcut, "Description", Description);
            sc.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            return true;
        }, false);
    }

    /// <summary>读出快捷方式指向的目标（读不到时返回 null）。</summary>
    private static string ReadTarget()
    {
        return WithShortcut(ShortcutPath, delegate(Type sc, object shortcut)
        {
            object value = sc.InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
            return value as string;
        }, null);
    }

    private static T WithShortcut<T>(string shortcutPath, ShortcutOperation<T> operation, T fallback)
    {
        if (string.IsNullOrEmpty(shortcutPath))
            return fallback;

        object shell = null;
        object shortcut = null;
        try
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return fallback;

            shell = Activator.CreateInstance(shellType);
            shortcut = shellType.InvokeMember(
                "CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            if (shortcut == null)
                return fallback;

            return operation(shortcut.GetType(), shortcut);
        }
        catch
        {
            return fallback;
        }
        finally
        {
            Release(shortcut);
            Release(shell);
        }
    }

    private static void Set(Type sc, object shortcut, string property, string value)
    {
        sc.InvokeMember(property, BindingFlags.SetProperty, null, shortcut, new object[] { value });
    }

    private static void Release(object comObject)
    {
        if (comObject != null && Marshal.IsComObject(comObject))
        {
            try
            {
                Marshal.FinalReleaseComObject(comObject);
            }
            catch
            {
            }
        }
    }
}
