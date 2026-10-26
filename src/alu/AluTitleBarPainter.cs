using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 标题栏：型号铭牌、窗口标题、最小化 / 关闭两颗微型键帽。
    /// </summary>
    internal static class AluTitleBarPainter
    {
        public static void Draw(Graphics g, AluMetrics m, AluFonts fonts, AluPanelState state,
            bool hoverMinimize, bool hoverClose)
        {
            DrawModelTag(g, m, fonts);
            DrawWindowTitle(g, m, fonts, state);
            DrawWindowControls(g, m, hoverMinimize, hoverClose);
        }

        private static void DrawModelTag(Graphics g, AluMetrics m, AluFonts fonts)
        {
            AluText.Draw(g, "ALU-01", fonts.ModelTag, AluPalette.ModelTagText, m.ModelTag,
                AluTextAlign.Left, 0f, AluPalette.Alpha("#FFFFFF", 0.82), 1f);
        }

        private static void DrawWindowTitle(Graphics g, AluMetrics m, AluFonts fonts, AluPanelState state)
        {
            string title = state == AluPanelState.Locked ? "键盘锁 — 已锁定" : "键盘锁";
            AluText.Draw(g, title, fonts.Title, AluPalette.TitleText, m.WindowTitle,
                AluTextAlign.Center, 0f, AluPalette.TitleTextShadow, 1f);
        }

        private static void DrawWindowControls(Graphics g, AluMetrics m, bool hoverMinimize, bool hoverClose)
        {
            RectangleF slot = m.WinSlot;

            // 下沉的铝槽
            AluSurface.FillVerticalGradient(
                g, slot,
                new Color[] { AluPalette.WinSlotTop, AluPalette.WinSlotBottom },
                new float[] { 0f, 1f });
            AluSurface.DrawInnerShadowTop(g, slot, m.WinSlotRadiusPx, AluPalette.Alpha("#000000", 0.45), 3f * m.Scale);

            AluSurface.DrawTopBand(g, slot, m.WinSlotRadiusPx, AluPalette.WinSlotBorderTop, 1f * m.Scale, 1f * m.Scale);
            AluSurface.DrawBottomBand(g, slot, m.WinSlotRadiusPx, AluPalette.WinSlotBorderBottom, 1f * m.Scale, 1f * m.Scale);

            DrawKey(g, m, m.MinimizeButton, hoverMinimize, false);
            DrawKey(g, m, m.CloseButton, hoverClose, true);
        }

        private static void DrawKey(Graphics g, AluMetrics m, RectangleF key, bool hovered, bool isClose)
        {
            float radius = m.WinKeyRadiusPx;

            AluSurface.DrawDropShadow(g, key, radius, AluPalette.ShadowWeak, 1f * m.Scale, 1.5f * m.Scale);

            Color top, mid, bottom, border, glyph;
            if (isClose && hovered)
            {
                top = AluPalette.CloseHoverTop;
                mid = AluPalette.CloseHoverMid;
                bottom = AluPalette.CloseHoverBottom;
                border = AluPalette.CloseHoverBorder;
                glyph = AluPalette.CloseHoverGlyph;
            }
            else if (hovered)
            {
                top = AluPalette.WinKeyHoverTop;
                mid = AluPalette.WinKeyHoverMid;
                bottom = AluPalette.WinKeyHoverBottom;
                border = AluPalette.WinKeyBorder;
                glyph = AluPalette.WinGlyph;
            }
            else
            {
                top = AluPalette.WinKeyTop;
                mid = AluPalette.WinKeyMid;
                bottom = AluPalette.WinKeyBottom;
                border = AluPalette.WinKeyBorder;
                glyph = AluPalette.WinGlyph;
            }

            AluSurface.FillVerticalGradient(
                g, key,
                new Color[] { top, mid, bottom },
                new float[] { 0f, 0.60f, 1f });

            AluSurface.DrawInnerTopLight(g, key, radius, AluPalette.Alpha("#FFFFFF", isClose && hovered ? 0.70 : 0.95), 1f * m.Scale);
            AluSurface.DrawInnerBottomEdge(g, key, radius, isClose && hovered ? AluPalette.CloseHoverInset : AluPalette.WinKeyInset, 1.5f * m.Scale);

            using (Pen pen = new Pen(border, 1f))
                AluSurface.DrawRounded(g, key, radius, pen);

            DrawGlyph(g, m, key, glyph, isClose);
        }

        private static void DrawGlyph(Graphics g, AluMetrics m, RectangleF key, Color color, bool isClose)
        {
            float cx = key.Left + key.Width / 2f;
            float cy = key.Top + key.Height / 2f;
            float s = m.Scale;

            using (Pen pen = new Pen(color, 1.6f * s))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                if (isClose)
                {
                    float r = 3f * s;
                    g.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                    g.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
                }
                else
                {
                    float r = 3.5f * s;
                    g.DrawLine(pen, cx - r, cy, cx + r, cy);
                }
            }
        }
    }
}
