using System;
using System.Drawing;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 面板配色：与 v1.3 的 CSS 设计稿一一对应（CSS 十六进制 → GDI+ Color）。
    /// 集中放在一处，方便和设计稿逐项比对；不要在绘制代码里散落硬编码颜色。
    ///
    /// 所有颜色与渐变 stops 都是进程生命周期内只解析一次的静态只读值：
    /// 绘制代码每帧直接引用，不再调用 <see cref="Html"/> / <see cref="Alpha"/>，也不再分配数组。
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

        public static readonly Color ChassisEdgeShadeLeft = Alpha("#19202A", 0.10);
        public static readonly Color ChassisEdgeShadeRight = Alpha("#19202A", 0.12);
        public static readonly Color ChassisEdgeLight = Alpha("#FFFFFF", 0.28);
        public static readonly Color ChassisEdgeFade = Alpha("#FFFFFF", 0.00);
        public static readonly Color ChassisBevelTop = Alpha("#FFFFFF", 0.98);
        public static readonly Color ChassisBevelSide = Alpha("#FFFFFF", 0.75);
        public static readonly Color ChassisBevelBottomLine = Alpha("#FFFFFF", 0.45);

        public static readonly Color[] ChassisStops = { ChassisTop, ChassisUpper, ChassisMid, ChassisLower, ChassisBottom };
        public static readonly float[] ChassisPositions = { 0f, 0.22f, 0.52f, 0.82f, 1f };
        public static readonly Color[] ChassisEdgeStops = { ChassisEdgeShadeLeft, ChassisEdgeLight, ChassisEdgeFade, ChassisEdgeFade, ChassisEdgeLight, ChassisEdgeShadeRight };
        public static readonly float[] ChassisEdgePositions = { 0f, 0.06f, 0.18f, 0.82f, 0.94f, 1f };

        // ---- 标题栏 ----
        public static readonly Color TitleBarTop = Alpha("#FFFFFF", 0.72);
        public static readonly Color TitleBarMiddle = Alpha("#DCE2EA", 0.45);
        public static readonly Color TitleBarBottom = Alpha("#B4BCC8", 0.38);
        public static readonly Color TitleBarBorder = Html("#828B98");
        public static readonly Color TitleBarBevel = Alpha("#FFFFFF", 0.98);
        public static readonly Color TitleBarUnderline = Alpha("#FFFFFF", 0.85);
        public static readonly Color TitleText = Html("#242A32");
        public static readonly Color ModelTagText = Html("#545D68");
        public static readonly Color ModelTagTextShadow = Alpha("#FFFFFF", 0.82);
        public static readonly Color TitleTextShadow = Alpha("#FFFFFF", 0.88);

        public static readonly Color[] TitleBarStops = { TitleBarTop, TitleBarMiddle, TitleBarBottom };
        public static readonly float[] TitleBarPositions = { 0f, 0.55f, 1f };

        // ---- 窗口控制键（最小化 / 关闭）----
        public static readonly Color WinSlotTop = Html("#7A8492");
        public static readonly Color WinSlotBottom = Html("#9AA4B2");
        public static readonly Color WinSlotBorderTop = Html("#5A626E");
        public static readonly Color WinSlotBorderBottom = Alpha("#FFFFFF", 0.90);
        public static readonly Color WinSlotInnerShadow = Alpha("#000000", 0.45);
        public static readonly Color WinKeyTop = Html("#FBFCFE");
        public static readonly Color WinKeyMid = Html("#E3E8EF");
        public static readonly Color WinKeyBottom = Html("#C9D0DA");
        public static readonly Color WinKeyBorder = Html("#4A525E");
        public static readonly Color WinKeyInset = Html("#AAB3C0");
        public static readonly Color WinKeyTopLight = Alpha("#FFFFFF", 0.95);
        public static readonly Color WinGlyph = Html("#343B45");
        public static readonly Color WinKeyHoverTop = Html("#FFFFFF");
        public static readonly Color WinKeyHoverMid = Html("#EEF1F5");
        public static readonly Color WinKeyHoverBottom = Html("#D6DCE5");

        public static readonly Color CloseHoverTop = Html("#E0523B");
        public static readonly Color CloseHoverMid = Html("#C83A24");
        public static readonly Color CloseHoverBottom = Html("#AB2A16");
        public static readonly Color CloseHoverBorder = Html("#3D0F07");
        public static readonly Color CloseHoverInset = Html("#7A190A");
        public static readonly Color CloseHoverTopLight = Alpha("#FFFFFF", 0.70);
        public static readonly Color CloseHoverGlyph = Html("#FFFFFF");

        public static readonly Color[] WinSlotStops = { WinSlotTop, WinSlotBottom };
        public static readonly Color[] WinKeyStops = { WinKeyTop, WinKeyMid, WinKeyBottom };
        public static readonly Color[] WinKeyHoverStops = { WinKeyHoverTop, WinKeyHoverMid, WinKeyHoverBottom };
        public static readonly Color[] CloseHoverStops = { CloseHoverTop, CloseHoverMid, CloseHoverBottom };
        public static readonly float[] WinKeyPositions = { 0f, 0.60f, 1f };

        // ---- 凹槽模块（滑块槽 + 状态窗的共同外框）----
        public static readonly Color ModuleTop = Html("#121417");
        public static readonly Color ModuleBottom = Html("#20242A");
        public static readonly Color ModuleBorderTop = Html("#0B0C0E");
        public static readonly Color ModuleBorderBottom = Alpha("#FFFFFF", 0.92);

        public static readonly Color[] ModuleStops = { ModuleTop, ModuleBottom };

        // ---- 机械滑块导轨 ----
        public static readonly Color RailTop = Html("#0D0F12");
        public static readonly Color RailBottom = Html("#1A1D22");
        public static readonly Color RailStripeLight = Alpha("#FFFFFF", 0.025);
        public static readonly Color RailStripeDark = Alpha("#000000", 0.22);
        public static readonly Color RailInnerShadow = Alpha("#000000", 0.95);
        public static readonly Color RailLabel = Html("#5C6470");

        public static readonly Color ThumbTop = Html("#F8FAFC");
        public static readonly Color ThumbMid = Html("#E4E9F0");
        public static readonly Color ThumbBottom = Html("#CBD2DC");
        public static readonly Color ThumbBorder = Html("#0B0D10");
        public static readonly Color ThumbInset = Html("#9AA3B0");
        public static readonly Color ThumbShadow = Alpha("#000000", 0.80);
        public static readonly Color ThumbTopLight = Alpha("#FFFFFF", 0.95);
        public static readonly Color ThumbText = Html("#2C333D");
        public static readonly Color ThumbTextShadow = Alpha("#FFFFFF", 0.95);
        public static readonly Color GripRidge = Html("#8D96A5");
        public static readonly Color GripRidgeHighlight = Alpha("#FFFFFF", 0.95);

        public static readonly Color[] RailStops = { RailTop, RailBottom };
        public static readonly Color[] ThumbStops = { ThumbTop, ThumbMid, ThumbBottom };
        public static readonly float[] ThumbPositions = { 0f, 0.56f, 1f };

        // ---- 状态显示窗 ----
        public static readonly Color StatusTop = Html("#121519");
        public static readonly Color StatusBottom = Html("#1B2026");
        public static readonly Color StatusBorder = Html("#090A0C");
        public static readonly Color StatusText = Html("#F3F4F6");
        public static readonly Color StatusEnUnlocked = Html("#6EE7B7");
        public static readonly Color StatusEnLocked = Html("#FCA5A5");
        public static readonly Color StatusEnUnavailable = Html("#FCD34D");

        public static readonly Color[] StatusStops = { StatusTop, StatusBottom };
        // 玻璃面板上的一道斜向反光：上半亮、下半暗，中间 0.5% 处硬切。
        public static readonly Color[] StatusGlassStops =
        {
            Alpha("#FFFFFF", 0.11), Alpha("#FFFFFF", 0.02), Alpha("#000000", 0.22), Alpha("#000000", 0.42)
        };
        public static readonly float[] StatusGlassPositions = { 0f, 0.42f, 0.425f, 1f };

        // ---- LED ----
        public static readonly Color LedBezelTop = Html("#7B8492");
        public static readonly Color LedBezelBottom = Html("#1F2328");
        public static readonly Color LedBezelHighlight = Alpha("#FFFFFF", 0.22);
        public static readonly Color LedBezelInnerShadow = Alpha("#000000", 0.92);
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

        public static readonly Color[] LedBezelStops = { LedBezelTop, LedBezelBottom };
        public static readonly Color[] LedGreenStops = { LedLensGreenCore, LedLensGreenMid, LedLensGreenEdge };
        public static readonly Color[] LedRedStops = { LedLensRedCore, LedLensRedMid, LedLensRedEdge };
        public static readonly Color[] LedAmberStops = { LedLensAmberCore, LedLensAmberMid, LedLensAmberEdge };
        public static readonly float[] LedLensPositions = { 0f, 0.45f, 1f };

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
        public static readonly Color MainKeyTopLight = Alpha("#FFFFFF", 0.95);
        public static readonly Color MainKeyText = Html("#262C34");
        public static readonly Color MainKeyTextShadow = Alpha("#FFFFFF", 0.90);
        public static readonly Color MainKeyDisabledText = Html("#7A828D");

        public static readonly Color MainKeyLockedTop = Html("#E0523B");
        public static readonly Color MainKeyLockedMid = Html("#C83A24");
        public static readonly Color MainKeyLockedBottom = Html("#AB2A16");
        public static readonly Color MainKeyLockedBorder = Html("#3D0F07");
        public static readonly Color MainKeyLockedInset = Html("#7A190A");
        public static readonly Color MainKeyLockedGlow = Alpha("#FFB9A5", 0.50);
        public static readonly Color MainKeyLockedTopLight = Alpha("#FFDCDA", 0.70);
        public static readonly Color MainKeyLockedTextShadow = Alpha("#5A0F05", 0.80);

        public static readonly Color[] MainSlotStops = { MainSlotTop, MainSlotBottom };
        public static readonly Color[] MainKeyStops = { MainKeyTop, MainKeyMid, MainKeyBottom };
        public static readonly float[] MainKeyPositions = { 0f, 0.55f, 1f };
        public static readonly Color[] MainKeyLockedStops = { MainKeyLockedTop, MainKeyLockedMid, MainKeyLockedBottom };
        public static readonly float[] MainKeyLockedPositions = { 0f, 0.58f, 1f };

        // ---- 页脚键帽 ----
        public static readonly Color KeyWellFill = Html("#788290");
        public static readonly Color KeyWellBorderTop = Html("#555D69");
        public static readonly Color KeyWellBorderBottom = Alpha("#FFFFFF", 0.90);
        public static readonly Color KeyWellInnerShadow = Alpha("#000000", 0.48);
        public static readonly Color KeycapTop = Html("#F8FAFC");
        public static readonly Color KeycapMid = Html("#D5DCE6");
        public static readonly Color KeycapBottom = Html("#B4BECC");
        public static readonly Color KeycapBorder = Html("#48505C");
        public static readonly Color KeycapShadow = Alpha("#000000", 0.30);
        public static readonly Color KeycapTopLight = Alpha("#FFFFFF", 0.95);
        public static readonly Color KeycapFaceTop = Html("#E1E7EF");
        public static readonly Color KeycapFaceBottom = Html("#F8FAFC");
        public static readonly Color KeycapFaceShadow = Alpha("#0F172A", 0.14);
        public static readonly Color KeycapFaceEdge = Alpha("#FFFFFF", 0.95);
        public static readonly Color KeycapText = Html("#262C34");
        public static readonly Color KeycapTextShadow = Alpha("#FFFFFF", 0.95);
        public static readonly Color PlusSign = Html("#4F5864");
        public static readonly Color PlusSignShadow = Alpha("#FFFFFF", 0.88);
        public static readonly Color EngravedNote = Html("#4B535F");
        public static readonly Color EngravedNoteShadow = Alpha("#FFFFFF", 0.90);

        public static readonly Color[] KeycapStops = { KeycapTop, KeycapMid, KeycapBottom };
        public static readonly float[] KeycapPositions = { 0f, 0.70f, 1f };
        public static readonly Color[] KeycapFaceStops = { KeycapFaceTop, KeycapFaceBottom };

        // ---- 通用 ----
        public static readonly Color ShadowStrong = Alpha("#000000", 0.88);
        public static readonly Color ShadowMedium = Alpha("#000000", 0.52);
        public static readonly Color ShadowSoft = Alpha("#000000", 0.36);
        public static readonly Color ShadowWeak = Alpha("#000000", 0.28);

        /// <summary>两段渐变最常用的位置表：首尾。</summary>
        public static readonly float[] TwoStopPositions = { 0f, 1f };

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
