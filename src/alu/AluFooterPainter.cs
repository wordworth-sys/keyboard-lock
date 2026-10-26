using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 页脚：Ctrl + Alt + U 三颗键帽（带凹陷井口）与右侧的蚀刻说明。
    /// </summary>
    internal static class AluFooterPainter
    {
        private const float MinKeycapWidth = 36f;   // CSS min-width: 38px（含边框）
        private const float KeycapPadding = 16f;    // CSS padding: 0 8px
        private const float WellPadding = 4f;       // CSS padding: 2px
        private const float WellBorder = 2f;        // CSS border: 1px
        private const float GroupGap = 5f;          // CSS gap: 5px

        public static void Draw(Graphics g, AluMetrics m, AluFonts fonts)
        {
            float s = m.Scale;

            float ctrlWidth = KeycapWidth(g, m, fonts, "Ctrl");
            float altWidth = KeycapWidth(g, m, fonts, "Alt");
            float uWidth = KeycapWidth(g, m, fonts, "U");

            float x = m.Footer.Left;

            x = DrawKey(g, m, fonts, x, ctrlWidth, "Ctrl");
            x = DrawPlus(g, m, fonts, x);
            x = DrawKey(g, m, fonts, x, altWidth, "Alt");
            x = DrawPlus(g, m, fonts, x);
            DrawKey(g, m, fonts, x, uWidth, "U");

            AluText.Draw(g, "关闭窗口后仍在托盘运行", fonts.NoteLabel, AluPalette.EngravedNote, m.EngravedNote,
                AluTextAlign.Right, 0f, AluPalette.EngravedNoteShadow, 1f);
        }

        private static float KeycapWidth(Graphics g, AluMetrics m, AluFonts fonts, string label)
        {
            float textWidth = AluText.MeasureWidth(g, label, fonts.KeyLabel, 0f) / m.Scale;
            return Math.Max(MinKeycapWidth, textWidth + KeycapPadding);
        }

        private static float DrawKey(Graphics g, AluMetrics m, AluFonts fonts, float x, float keycapWidth, string label)
        {
            float s = m.Scale;
            float wellWidth = keycapWidth + WellPadding + WellBorder;
            float wellHeight = 28f * s + WellPadding * s + WellBorder * s;

            RectangleF well = new RectangleF(x, m.Footer.Top, wellWidth, wellHeight);

            // 井口
            using (SolidBrush wellBrush = new SolidBrush(AluPalette.KeyWellFill))
                AluSurface.FillRounded(g, well, m.KeyWellRadiusPx, wellBrush);
            AluSurface.DrawInnerShadowTop(g, well, m.KeyWellRadiusPx, AluPalette.Alpha("#000000", 0.48), 3f * s);

            AluSurface.DrawTopBand(g, well, m.KeyWellRadiusPx, AluPalette.KeyWellBorderTop, 1f * s, 1f * s);
            AluSurface.DrawBottomBand(g, well, m.KeyWellRadiusPx, AluPalette.KeyWellBorderBottom, 1f * s, 1f * s);

            // 键帽
            RectangleF cap = new RectangleF(
                well.X + (WellPadding / 2f + WellBorder / 2f) * s,
                well.Y + (WellPadding / 2f + WellBorder / 2f) * s,
                keycapWidth * s,
                28f * s);

            AluSurface.DrawDropShadow(g, cap, m.KeycapRadiusPx, AluPalette.Alpha("#000000", 0.30), 2f * s, 3f * s);

            AluSurface.FillVerticalGradient(
                g, cap,
                new Color[] { AluPalette.KeycapTop, AluPalette.KeycapMid, AluPalette.KeycapBottom },
                new float[] { 0f, 0.70f, 1f });

            AluSurface.DrawInnerTopLight(g, cap, m.KeycapRadiusPx, AluPalette.Alpha("#FFFFFF", 0.95), 1f * s);

            // 键帽顶面的二次曲面（CSS: .keycap::before）
            RectangleF face = new RectangleF(
                cap.X + 1f * s + 3.5f * s,
                cap.Y + 1f * s + 2.5f * s,
                cap.Width - 2f * s - 7f * s,
                cap.Height - 1f * s - 2.5f * s - 5f * s);

            if (face.Width > 0f && face.Height > 0f)
            {
                AluSurface.FillRoundedVerticalGradient(
                    g, face, 3.5f * s,
                    new Color[] { AluPalette.KeycapFaceTop, AluPalette.KeycapFaceBottom },
                    new float[] { 0f, 1f });

                AluSurface.DrawInnerShadowTop(g, face, 3.5f * s, AluPalette.Alpha("#0F172A", 0.14), 2f * s);
                AluSurface.DrawInnerBottomEdge(g, face, 3.5f * s, AluPalette.Alpha("#FFFFFF", 0.95), 1f * s);
            }

            using (Pen pen = new Pen(AluPalette.KeycapBorder, 1f))
                AluSurface.DrawRounded(g, cap, m.KeycapRadiusPx, pen);

            AluText.Draw(g, label, fonts.KeyLabel, AluPalette.KeycapText, cap,
                AluTextAlign.Center, 0f, AluPalette.Alpha("#FFFFFF", 0.95), 1f);

            return well.Right + GroupGap * s;
        }

        private static float DrawPlus(Graphics g, AluMetrics m, AluFonts fonts, float x)
        {
            float s = m.Scale;
            float width = 8f * s;
            float height = 14f * s;

            RectangleF rect = new RectangleF(x, m.Footer.Top + (m.Footer.Height - height) / 2f, width, height);
            AluText.Draw(g, "+", fonts.KeyLabel, AluPalette.PlusSign, rect,
                AluTextAlign.Center, 0f, AluPalette.Alpha("#FFFFFF", 0.88), 1f);

            return rect.Right + GroupGap * s;
        }
    }
}
