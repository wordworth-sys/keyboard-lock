using System;
using System.Drawing;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 面板配色：与 v1.3 的 CSS 设计稿一一对应（CSS 十六进制 → GDI+ Color）。
    /// 集中放在一处，方便和设计稿逐项比对；不要在绘制代码里散落硬编码颜色。
    /// </summary>
    internal static class AluPalette
    {
        // ---- 机身（铝制外壳）----
        public static readonly Color ChassisTop = Html("#F0F3F7");
        public static readonly Color ChassisUpper = Html("#E1E6ED");
        public static readonly Color ChassisMid = Html("#D3D9E2");
        public static readonly Color ChassisLower = Html("#C1C8D3");
        public static readonly Color ChassisBottom = Html("#AEB6C3");
        public static readonly Color ChassisBorder = Html("#68707C");
        public static readonly Color ChassisBottomRidge = Html("#7A8390");

        // ---- 标题栏 ----
        public static readonly Color TitleBarTop = Alpha("#FFFFFF", 0.72);
        public static readonly Color TitleBarMiddle = Alpha("#DCE2EA", 0.45);
        public static readonly Color TitleBarBottom = Alpha("#B4BCC8", 0.38);
        public static readonly Color TitleBarBorder = Html("#828B98");
        public static readonly Color TitleText = Html("#242A32");
        public static readonly Color ModelTagText = Html("#545D68");
        public static readonly Color TitleTextShadow = Alpha("#FFFFFF", 0.88);

        // ---- 窗口控制键（最小化 / 关闭）----
        public static readonly Color WinSlotTop = Html("#7A8492");
        public static readonly Color WinSlotBottom = Html("#9AA4B2");
        public static readonly Color WinSlotBorderTop = Html("#5A626E");
        public static readonly Color WinSlotBorderBottom = Alpha("#FFFFFF", 0.90);
        public static readonly Color WinKeyTop = Html("#FBFCFE");
        public static readonly Color WinKeyMid = Html("#E3E8EF");
        public static readonly Color WinKeyBottom = Html("#C9D0DA");
        public static readonly Color WinKeyBorder = Html("#4A525E");
        public static readonly Color WinKeyInset = Html("#AAB3C0");
        public static readonly Color WinGlyph = Html("#343B45");
        public static readonly Color WinKeyHoverTop = Html("#FFFFFF");
        public static readonly Color WinKeyHoverMid = Html("#EEF1F5");
        public static readonly Color WinKeyHoverBottom = Html("#D6DCE5");

        public static readonly Color CloseHoverTop = Html("#E0523B");
        public static readonly Color CloseHoverMid = Html("#C83A24");
        public static readonly Color CloseHoverBottom = Html("#AB2A16");
        public static readonly Color CloseHoverBorder = Html("#3D0F07");
        public static readonly Color CloseHoverInset = Html("#7A190A");
        public static readonly Color CloseHoverGlyph = Html("#FFFFFF");

        // ---- 凹槽模块（滑块槽 + 状态窗的共同外框）----
        public static readonly Color ModuleTop = Html("#121417");
        public static readonly Color ModuleBottom = Html("#20242A");
        public static readonly Color ModuleBorderTop = Html("#0B0C0E");
        public static readonly Color ModuleBorderBottom = Alpha("#FFFFFF", 0.92);

        // ---- 机械滑块导轨 ----
        public static readonly Color RailTop = Html("#0D0F12");
        public static readonly Color RailBottom = Html("#1A1D22");
        public static readonly Color RailStripeLight = Alpha("#FFFFFF", 0.025);
        public static readonly Color RailStripeDark = Alpha("#000000", 0.22);
        public static readonly Color RailLabel = Html("#5C6470");

        public static readonly Color ThumbTop = Html("#F8FAFC");
        public static readonly Color ThumbMid = Html("#E4E9F0");
        public static readonly Color ThumbBottom = Html("#CBD2DC");
        public static readonly Color ThumbBorder = Html("#0B0D10");
        public static readonly Color ThumbInset = Html("#9AA3B0");
        public static readonly Color ThumbText = Html("#2C333D");
        public static readonly Color GripRidge = Html("#8D96A5");

        // ---- 状态显示窗 ----
        public static readonly Color StatusTop = Html("#121519");
        public static readonly Color StatusBottom = Html("#1B2026");
        public static readonly Color StatusBorder = Html("#090A0C");
        public static readonly Color StatusText = Html("#F3F4F6");
        public static readonly Color StatusEnUnlocked = Html("#6EE7B7");
        public static readonly Color StatusEnLocked = Html("#FCA5A5");
        public static readonly Color StatusEnUnavailable = Html("#FCD34D");

        // ---- LED ----
        public static readonly Color LedBezelTop = Html("#7B8492");
        public static readonly Color LedBezelBottom = Html("#1F2328");
        public static readonly Color LedLensGreenCore = Html("#A7F3D0");
        public static readonly Color LedLensGreenMid = Html("#10B981");
        public static readonly Color LedLensGreenEdge = Html("#022C22");
        public static readonly Color LedLensRedCore = Html("#FECACA");
        public static readonly Color LedLensRedMid = Html("#EF4444");
        public static readonly Color LedLensRedEdge = Html("#450A0A");
        public static readonly Color LedLensAmberCore = Html("#FDE68A");
        public static readonly Color LedLensAmberMid = Html("#F59E0B");
        public static readonly Color LedLensAmberEdge = Html("#451A03");
        public static readonly Color LedHighlight = Alpha("#FFFFFF", 0.92);

        // ---- 主操作键 ----
        public static readonly Color MainSlotTop = Html("#6E7784");
        public static readonly Color MainSlotBottom = Html("#99A3B1");
        public static readonly Color MainSlotBorderTop = Html("#525965");
        public static readonly Color MainSlotBorderBottom = Alpha("#FFFFFF", 0.92);

        public static readonly Color MainKeyTop = Html("#FBFCFE");
        public static readonly Color MainKeyMid = Html("#EBEFF5");
        public static readonly Color MainKeyBottom = Html("#D8DFE8");
        public static readonly Color MainKeyBorder = Html("#454C56");
        public static readonly Color MainKeyInset = Html("#B0B9C6");
        public static readonly Color MainKeyText = Html("#262C34");
        public static readonly Color MainKeyTextShadow = Alpha("#FFFFFF", 0.90);
        public static readonly Color MainKeyDisabledText = Html("#7A828D");

        public static readonly Color MainKeyLockedTop = Html("#E0523B");
        public static readonly Color MainKeyLockedMid = Html("#C83A24");
        public static readonly Color MainKeyLockedBottom = Html("#AB2A16");
        public static readonly Color MainKeyLockedBorder = Html("#3D0F07");
        public static readonly Color MainKeyLockedInset = Html("#7A190A");
        public static readonly Color MainKeyLockedGlow = Alpha("#FFB9A5", 0.50);

        // ---- 页脚键帽 ----
        public static readonly Color KeyWellFill = Html("#788290");
        public static readonly Color KeyWellBorderTop = Html("#555D69");
        public static readonly Color KeyWellBorderBottom = Alpha("#FFFFFF", 0.90);
        public static readonly Color KeycapTop = Html("#F8FAFC");
        public static readonly Color KeycapMid = Html("#D5DCE6");
        public static readonly Color KeycapBottom = Html("#B4BECC");
        public static readonly Color KeycapBorder = Html("#48505C");
        public static readonly Color KeycapFaceTop = Html("#E1E7EF");
        public static readonly Color KeycapFaceBottom = Html("#F8FAFC");
        public static readonly Color KeycapText = Html("#262C34");
        public static readonly Color PlusSign = Html("#4F5864");
        public static readonly Color EngravedNote = Html("#4B535F");
        public static readonly Color EngravedNoteShadow = Alpha("#FFFFFF", 0.90);

        // ---- 通用阴影 ----
        public static readonly Color ShadowStrong = Alpha("#000000", 0.88);
        public static readonly Color ShadowMedium = Alpha("#000000", 0.52);
        public static readonly Color ShadowSoft = Alpha("#000000", 0.36);
        public static readonly Color ShadowWeak = Alpha("#000000", 0.28);

        /// <summary>CSS 十六进制色（含 #）→ Color。</summary>
        public static Color Html(string hex)
        {
            return ColorTranslator.FromHtml(hex);
        }

        /// <summary>给 CSS 十六进制色附加透明度（alpha 取 0~1）。</summary>
        public static Color Alpha(string hex, double alpha)
        {
            Color c = ColorTranslator.FromHtml(hex);
            int a = (int)Math.Round(Math.Max(0.0, Math.Min(1.0, alpha)) * 255.0);
            return Color.FromArgb(a, c.R, c.G, c.B);
        }
    }
}
