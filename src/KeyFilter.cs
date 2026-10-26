using System;

/// <summary>
/// 锁键盘的按键决策状态机：输入一次低层按键事件，输出 放行 / 吞掉 / 切换锁定并吞掉。
/// 纯托管，不调用任何 Win32 API，热路径零分配，可以脱离钩子单独做单元测试（见 tests/）。
///
/// 核心规则——系统看到的每个键永远是成对的 按下/抬起：
///   · KEYDOWN 放行过的键，它的 KEYUP 一定放行：锁定瞬间正按着的 Ctrl/Alt 松开时能送达，不会卡键
///     （否则滚轮会变成 Ctrl+滚轮，点击变成 Ctrl+点击）；
///   · KEYDOWN 被吞掉的键，它的自动重复和 KEYUP 也一律吞掉：解锁时按住 Ctrl+Alt+U 不放，
///     不会漏出 "uuu"，也不会漏出孤立的 Alt 抬起去激活前台程序的菜单栏。
/// </summary>
internal sealed class KeyFilter
{
    public const int Pass = 0;
    public const int Block = 1;
    public const int ToggleLock = 2;

    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4;
    private const int VK_RMENU = 0xA5;
    private const int VK_U = 0x55;

    // AltGr 布局下系统会在右 Alt 前伪造一个左 Ctrl，其扫描码带 0x200 标记（0x21D）。
    // 它单独记在一个未被任何虚拟键使用的槽位里，照常成对放行/吞掉，但不算作“按住了 Ctrl”。
    private const int AltGrFakeCtrlScanFlag = 0x200;
    private const int SlotAltGrFakeCtrl = 0x07;

    // 正常按住任何键时，硬件自动重复每 30~1000ms 就会产生一次事件；
    // 即便关闭了自动重复或慢速按组合键，5 秒窗口也足以覆盖正常的 Ctrl+Alt+U 操作。
    // 若连续超过 5 秒没有任何按键事件，说明此前记录的“按住”状态很可能已在 UAC / Ctrl+Alt+Del /
    // 管理员窗口里松开（普通权限钩子收不到那里的 KEYUP），应视为已抬起，避免之后单按 U 误触发。
    private const uint StaleHoldResetMs = 5000;

    private readonly uint[] held = new uint[8];   // 物理上按着的键（256 位）
    private readonly uint[] passed = new uint[8]; // KEYDOWN 已放行给系统、尚未放行 KEYUP 的键
    private uint lastEventTime;
    private bool hasEvent;

    /// <summary>清空当前记录的物理按住状态（用于会话切换、锁屏恢复或重装钩子时消除残留）。</summary>
    public void ResetHeld()
    {
        Array.Clear(held, 0, held.Length);
        hasEvent = false;
    }

    /// <summary>处理一次按键事件。time 取 KBDLLHOOKSTRUCT.time（毫秒，允许回绕）。</summary>
    public int Process(int vk, int scanCode, bool isUp, uint time, bool locked)
    {
        if (hasEvent && unchecked(time - lastEventTime) > StaleHoldResetMs)
            Array.Clear(held, 0, held.Length);

        hasEvent = true;
        lastEventTime = time;

        int slot = (vk == VK_LCONTROL && (scanCode & AltGrFakeCtrlScanFlag) != 0)
            ? SlotAltGrFakeCtrl
            : (vk & 0xFF);

        if (isUp)
        {
            Clear(held, slot);
            if (!Get(passed, slot))
                return Block;
            Clear(passed, slot);
            return Pass;
        }

        if (Get(held, slot))
            return (!locked && Get(passed, slot)) ? Pass : Block;

        Set(held, slot);

        if (vk == VK_U && IsCtrlHeld() && IsAltHeld())
        {
            Clear(passed, slot);
            return ToggleLock;
        }

        if (locked)
        {
            Clear(passed, slot);
            return Block;
        }

        Set(passed, slot);
        return Pass;
    }

    private bool IsCtrlHeld()
    {
        return Get(held, VK_LCONTROL) || Get(held, VK_RCONTROL) || Get(held, VK_CONTROL);
    }

    private bool IsAltHeld()
    {
        return Get(held, VK_LMENU) || Get(held, VK_RMENU) || Get(held, VK_MENU);
    }

    private static bool Get(uint[] bits, int i)
    {
        return (bits[i >> 5] & (1u << (i & 31))) != 0;
    }

    private static void Set(uint[] bits, int i)
    {
        bits[i >> 5] |= 1u << (i & 31);
    }

    private static void Clear(uint[] bits, int i)
    {
        bits[i >> 5] &= ~(1u << (i & 31));
    }
}
