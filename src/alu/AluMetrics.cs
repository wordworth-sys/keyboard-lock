using System;
using System.Drawing;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 面板布局度量：设计稿坐标系为 400×264，运行时按 DPI 缩放到实际像素。
    /// 绘制模块优先使用这里的矩形；需要按字形动态排布的局部（如页脚键帽）在各自的 Painter 内计算。
    ///
    /// 矩形均以「面板左上角」为原点，单位为设备像素（已含缩放）。
    /// </summary>
    public sealed class AluMetrics
    {
        // ---- 设计稿常量 ----
        public const float DesignWidth = 400f;
        public const float DesignHeight = 264f;

        private const float ChassisRadius = 16f;
        private const float ModuleRadius = 12f;
        private const float BayRadius = 8f;
        private const float StatusRadius = 8f;
        private const float ThumbRadius = 6f;
        private const float MainSlotRadius = 12f;
        private const float MainKeyRadius = 9f;
        private const float WinSlotRadius = 7f;
        private const float WinKeyRadius = 5f;
        private const float KeyWellRadius = 7f;
        private const float KeycapRadius = 5f;

        public readonly float Scale;

        public readonly RectangleF Chassis;
        public readonly RectangleF ChassisContent;
        public readonly RectangleF TitleBar;

        public readonly RectangleF ModelTag;
        public readonly RectangleF WindowTitle;
        public readonly RectangleF WinSlot;
        public readonly RectangleF MinimizeButton;
        public readonly RectangleF CloseButton;

        public readonly RectangleF Module;
        public readonly RectangleF SliderBay;
        public readonly RectangleF StatusWindow;
        public readonly RectangleF Rail;
        public readonly RectangleF ThumbFree;
        public readonly RectangleF ThumbHold;

        public readonly RectangleF LedBezel;
        public readonly RectangleF LedLens;
        public readonly RectangleF StatusTextCn;
        public readonly RectangleF StatusTextEn;

        public readonly RectangleF MainSlot;
        public readonly RectangleF MainButton;

        public readonly RectangleF Footer;
        public readonly RectangleF EngravedNote;

        public readonly float ChassisRadiusPx;
        public readonly float ModuleRadiusPx;
        public readonly float BayRadiusPx;
        public readonly float StatusRadiusPx;
        public readonly float ThumbRadiusPx;
        public readonly float MainSlotRadiusPx;
        public readonly float MainKeyRadiusPx;
        public readonly float WinSlotRadiusPx;
        public readonly float WinKeyRadiusPx;
        public readonly float KeyWellRadiusPx;
        public readonly float KeycapRadiusPx;

        private AluMetrics(float scale, Size size)
        {
            Scale = scale;

            Chassis = new RectangleF(0f, 0f, size.Width, size.Height);
            ChassisContent = new RectangleF(1f * scale, 1f * scale, (DesignWidth - 2f) * scale, (DesignHeight - 2f) * scale);
            TitleBar = new RectangleF(1f * scale, 1f * scale, (DesignWidth - 2f) * scale, 36f * scale);

            ModelTag = Rect(15f, 12f, 60f, 14f, scale);
            WindowTitle = Rect(74f, 12f, 252f, 14f, scale);

            WinSlot = Rect(331f, 8f, 54f, 22f, scale);
            MinimizeButton = Rect(333f, 10f, 24f, 18f, scale);
            CloseButton = Rect(359f, 10f, 24f, 18f, scale);

            Module = Rect(19f, 52f, 362f, 84f, scale);
            SliderBay = Rect(23f, 57f, 174f, 74f, scale);
            StatusWindow = Rect(203f, 57f, 174f, 74f, scale);

            Rail = Rect(27f, 61f, 166f, 66f, scale);
            ThumbFree = Rect(27f, 61f, 120f, 66f, scale);
            ThumbHold = Rect(73f, 61f, 120f, 66f, scale);

            LedBezel = Rect(217f, 85f, 18f, 18f, scale);
            LedLens = Rect(220f, 88f, 12f, 12f, scale);
            StatusTextCn = Rect(248f, 78f, 120f, 19f, scale);
            StatusTextEn = Rect(248f, 97f, 120f, 12f, scale);

            MainSlot = Rect(19f, 149f, 362f, 63f, scale);
            MainButton = Rect(22f, 153f, 356f, 54f, scale);

            // 页脚的三颗键帽按字形宽度现场排布（见 AluFooterPainter），这里只给出页脚区与右侧说明。
            Footer = Rect(19f, 224f, 362f, 34f, scale);
            EngravedNote = Rect(240f, 232f, 140f, 14f, scale);

            ChassisRadiusPx = ChassisRadius * scale;
            ModuleRadiusPx = ModuleRadius * scale;
            BayRadiusPx = BayRadius * scale;
            StatusRadiusPx = StatusRadius * scale;
            ThumbRadiusPx = ThumbRadius * scale;
            MainSlotRadiusPx = MainSlotRadius * scale;
            MainKeyRadiusPx = MainKeyRadius * scale;
            WinSlotRadiusPx = WinSlotRadius * scale;
            WinKeyRadiusPx = WinKeyRadius * scale;
            KeyWellRadiusPx = KeyWellRadius * scale;
            KeycapRadiusPx = KeycapRadius * scale;
        }

        /// <summary>按实际控件尺寸换算度量；宽度是唯一缩放基准（面板固定 400:264）。</summary>
        public static AluMetrics FromSize(Size size)
        {
            float scale = size.Width <= 0 ? 1f : size.Width / DesignWidth;
            if (scale <= 0f)
                scale = 1f;
            return new AluMetrics(scale, size);
        }

        private static RectangleF Rect(float x, float y, float w, float h, float scale)
        {
            return new RectangleF(x * scale, y * scale, w * scale, h * scale);
        }
    }
}
