// KeyFilter 状态机单元测试。运行：.\tests\run-tests.ps1
using System;
using System.Collections.Generic;

internal static class KeyFilterTests
{
    const int P = KeyFilter.Pass, B = KeyFilter.Block, T = KeyFilter.ToggleLock;
    const int LCTRL = 0xA2, RCTRL = 0xA3, LALT = 0xA4, RALT = 0xA5, U = 0x55, A = 0x41, LSHIFT = 0xA0;
    const int SC_LCTRL = 0x1D, SC_FAKE_LCTRL = 0x21D;

    static int failures;

    /// <summary>模拟钩子：维护锁定状态与时间，逐个喂事件并核对结果。</summary>
    sealed class Rig
    {
        readonly KeyFilter f = new KeyFilter();
        public bool Locked;
        public uint Now;
        readonly string name;
        public Rig(string name, bool locked, uint start = 1000) { this.name = name; Locked = locked; Now = start; }

        public Rig Down(int vk, int expect, uint dt = 30, int scan = 0) { return Feed(vk, scan, false, expect, dt); }
        public Rig Up(int vk, int expect, uint dt = 30, int scan = 0) { return Feed(vk, scan, true, expect, dt); }
        public Rig Repeat(int vk, int times, int expect, uint dt = 33)
        {
            for (int i = 0; i < times; i++) Feed(vk, 0, false, expect, dt);
            return this;
        }

        Rig Feed(int vk, int scan, bool up, int expect, uint dt)
        {
            Now = unchecked(Now + dt);
            int got = f.Process(vk, scan, up, Now, Locked);
            if (got == T) Locked = !Locked;
            if (got != expect)
            {
                failures++;
                Console.WriteLine("  FAIL [" + name + "] vk=0x" + vk.ToString("X2") + (up ? " up" : " down")
                    + " expected " + N(expect) + " got " + N(got));
            }
            return this;
        }

        public Rig AssertLocked(bool expect)
        {
            if (Locked != expect) { failures++; Console.WriteLine("  FAIL [" + name + "] locked=" + Locked + " expected " + expect); }
            return this;
        }

        static string N(int a) { return a == P ? "Pass" : a == B ? "Block" : "Toggle"; }
    }

    static void Main()
    {
        var tests = new List<KeyValuePair<string, Action>>
        {
            Case("B1 解锁时按住 Ctrl+Alt+U 不放：重复与抬起全部吞掉", () =>
                new Rig("B1", locked: true)
                    .Down(LCTRL, B).Down(LALT, B).Down(U, T, 30)
                    .Repeat(U, 20, B)
                    .Up(U, B).Up(LALT, B).Up(LCTRL, B)
                    .AssertLocked(false)),

            Case("加锁：Ctrl/Alt 按下已放行，其抬起也必须放行（不卡键）", () =>
                new Rig("lock", locked: false)
                    .Down(LCTRL, P).Down(LALT, P).Down(U, T)
                    .Repeat(U, 20, B)
                    .Up(U, B).Up(LALT, P).Up(LCTRL, P)
                    .AssertLocked(true)),

            Case("鼠标点锁定时正按着 Ctrl：重复吞掉，抬起放行", () =>
            {
                var r = new Rig("mouse-lock", locked: false).Down(LCTRL, P);
                r.Locked = true;
                r.Repeat(LCTRL, 5, B).Up(LCTRL, P);
            }),

            Case("鼠标点解锁时正按着 A：之后的重复与抬起都不漏给前台", () =>
            {
                var r = new Rig("mouse-unlock", locked: true).Down(A, B);
                r.Locked = false;
                r.Repeat(A, 5, B).Up(A, B).Down(A, P).Up(A, P);
            }),

            Case("B3 左右 Ctrl 同按、松开其一，组合键依然有效", () =>
                new Rig("B3", locked: false)
                    .Down(LCTRL, P).Down(RCTRL, P).Up(LCTRL, P)
                    .Down(LALT, P).Down(U, T)
                    .AssertLocked(true)),

            Case("右 Ctrl + 右 Alt + U 同样有效", () =>
                new Rig("right", locked: true).Down(RCTRL, B).Down(RALT, B).Down(U, T).AssertLocked(false)),

            Case("B2 AltGr（伪造左 Ctrl + 右 Alt）+ U 不触发锁定", () =>
                new Rig("B2", locked: false)
                    .Down(LCTRL, P, scan: SC_FAKE_LCTRL).Down(RALT, P).Down(U, P)
                    .Up(U, P).Up(RALT, P).Up(LCTRL, P, scan: SC_FAKE_LCTRL)
                    .AssertLocked(false)),

            Case("真实左 Ctrl（扫描码 0x1D）不受 AltGr 过滤影响", () =>
                new Rig("real-lctrl", locked: false).Down(LCTRL, P, scan: SC_LCTRL).Down(LALT, P).Down(U, T)),

            Case("抬起事件丢失（UAC / Ctrl+Alt+Del）后，单按 U 不会误解锁", () =>
                new Rig("stale-locked", locked: true)
                    .Down(LCTRL, B).Down(LALT, B).Down(0x2E /*Del*/, B)
                    .Down(U, B, dt: 8000).Up(U, B)
                    .AssertLocked(true)),

            Case("抬起事件丢失（管理员窗口）后，未锁定时 Ctrl+U 不会误锁", () =>
                new Rig("stale-unlocked", locked: false)
                    .Down(LALT, P).Down(0x09 /*Tab*/, P)
                    .Down(LCTRL, P, dt: 9000).Down(U, P).Up(U, P).Up(LCTRL, P)
                    .AssertLocked(false)),

            Case("长按 Ctrl（持续自动重复）10 秒后再按 Alt+U 仍有效", () =>
                new Rig("long-hold", locked: false)
                    .Down(LCTRL, P).Repeat(LCTRL, 300, P)
                    .Down(LALT, P).Down(U, T)),

            Case("同时长按 Ctrl+Alt（仅 Alt 自动重复）10 秒后再按 U 仍有效", () =>
                new Rig("long-hold-chord", locked: false)
                    .Down(LCTRL, P).Down(LALT, P).Repeat(LALT, 300, P)
                    .Down(U, T).AssertLocked(true)),

            Case("按住 Ctrl+Alt 连点两次 U：锁 → 解", () =>
                new Rig("double", locked: false)
                    .Down(LCTRL, P).Down(LALT, P)
                    .Down(U, T).Up(U, B).AssertLocked(true)
                    .Down(U, T, 150).Up(U, B).AssertLocked(false)
                    .Up(LALT, P).Up(LCTRL, P)),

            Case("Ctrl+Alt+Shift+U 也能切换（Shift 不影响）", () =>
                new Rig("shift", locked: true).Down(LCTRL, B).Down(LSHIFT, B).Down(LALT, B).Down(U, T)),

            Case("未锁定时普通打字全部放行，单独 U 不触发", () =>
                new Rig("typing", locked: false)
                    .Down(A, P).Up(A, P).Down(U, P).Repeat(U, 5, P).Up(U, P)
                    .Down(LCTRL, P).Down(U, P).Up(U, P).Up(LCTRL, P)),

            Case("锁定时所有按键吞掉，Ctrl+Alt 不带 U 不解锁", () =>
                new Rig("locked-typing", locked: true)
                    .Down(A, B).Up(A, B).Down(0x5B /*LWin*/, B).Up(0x5B, B)
                    .Down(LCTRL, B).Down(LALT, B).Up(LALT, B).Up(LCTRL, B)
                    .AssertLocked(true)),

            Case("时间戳回绕（49.7 天）不影响判定", () =>
                new Rig("wrap", locked: false, start: uint.MaxValue - 40)
                    .Down(LCTRL, P).Down(LALT, P).Down(U, T).Repeat(U, 3, B).Up(U, B)),

            Case("慢速组合键（Ctrl → 2.5s → Alt → 2.0s → U，无自动重复）仍能正常切换", () =>
                new Rig("slow-chord-unlocked", locked: false)
                    .Down(LCTRL, P, dt: 30)
                    .Down(LALT, P, dt: 2500)
                    .Down(U, T, dt: 2000)
                    .Up(U, B).Up(LALT, P).Up(LCTRL, P)
                    .AssertLocked(true)),

            Case("锁定态下按住 Ctrl+Alt 停顿 3.5 秒（无中间重复事件）再按 U 仍能解锁", () =>
                new Rig("slow-chord-locked", locked: true)
                    .Down(LCTRL, B, dt: 30)
                    .Down(LALT, B, dt: 30)
                    .Down(U, T, dt: 3500)
                    .Up(U, B).Up(LALT, B).Up(LCTRL, B)
                    .AssertLocked(false)),

            Case("未锁定时钩子安装前已按下的键（Enter / Shift）：抬起放行，不卡键", () =>
                new Rig("pre-hook-up", locked: false)
                    .Up(0x0D /*Enter*/, P).Up(LSHIFT, P)
                    .Down(A, P).Up(A, P)),

            Case("锁定时钩子安装前已按下的键：修饰键抬起放行防卡键，普通键抬起照常吞掉", () =>
                new Rig("pre-hook-up-locked", locked: true)
                    .Up(0x0D /*Enter*/, B).Up(0x20 /*Space*/, B)
                    .Up(LSHIFT, P).Up(LCTRL, P).Up(LALT, P).Up(0x5B /*LWin*/, P)
                    .AssertLocked(true)),

            Case("锁定时从 UAC / 管理员窗口切回后松开残留按键：修饰键放行，普通键吞掉", () =>
                new Rig("uac-return-up-locked", locked: true)
                    .Up(RALT, P).Up(LCTRL, P, scan: SC_FAKE_LCTRL).Up(RCTRL, P).Up(0xA1 /*RShift*/, P).Up(0x5C /*RWin*/, P)
                    .Up(A, B).Up(0x0D /*Enter*/, B)
                    .AssertLocked(true)),

            Case("5 秒静默清掉 held 后解锁，此前被吞过 KEYDOWN 的键其抬起仍然吞掉（不漏孤立抬起）", () =>
                new Rig("swallowed-survives-stale", locked: true)
                    .Down(A, B)
                    .Down(LCTRL, B, dt: 8000).Down(LALT, B).Down(U, T).Up(U, B)
                    .Up(A, B)
                    .Up(LALT, B).Up(LCTRL, B)
                    .AssertLocked(false)),

            Case("抬起丢失后同一键再次按下：放行记录覆盖旧的吞掉记录", () =>
                new Rig("swallowed-then-pass", locked: true)
                    .Down(A, B)
                    .Down(LCTRL, B, dt: 8000).Down(LALT, B).Down(U, T)
                    .Down(A, P, dt: 8000).Up(A, P)
                    .AssertLocked(false)),
        };

        foreach (var t in tests)
        {
            int before = failures;
            t.Value();
            Console.WriteLine((failures == before ? "  ok   " : "  FAIL ") + t.Key);
        }

        Console.WriteLine(failures == 0 ? "\nAll " + tests.Count + " tests passed." : "\n" + failures + " assertion(s) failed.");
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    static KeyValuePair<string, Action> Case(string name, Action body) { return new KeyValuePair<string, Action>(name, body); }
}
