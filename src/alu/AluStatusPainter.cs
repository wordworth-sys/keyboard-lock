using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 状态显示窗：玻璃面板、状态指示灯（绿 / 红 / 琥珀）、中文状态与英文小标签。
    /// </summary>
    internal static class AluStatusPainter
    {
        public static void Draw(Graphics g, AluMetrics m, AluFonts fonts, AluPanelState state)
        {
            RectangleF window = m.StatusWindow;

            // 玻璃面板：底色渐变 + 一道斜向反光
            AluSurface.FillVerticalGradient(
                g, window,
                new Color[] { AluPalette.StatusTop, AluPalette.StatusBottom },
                new float[] { 0f, 1f });

            AluSurface.FillVerticalGradient(
                g, window,
                new Color[]
                {
                    AluPalette.Alpha("#FFFFFF", 0.11),
                    AluPalette.Alpha("#FFFFFF", 0.02),
                    AluPalette.Alpha("#000000", 0.22),
                    AluPalette.Alpha("#000000", 0.42)
                },
                new float[] { 0f, 0.42f, 0.425f, 1f });

            AluSurface.DrawInnerShadowTop(g, window, m.StatusRadiusPx, AluPalette.Alpha("#000000", 0.88), 6f * m.Scale);

            using (Pen pen = new Pen(AluPalette.StatusBorder, 1f))
                AluSurface.DrawRounded(g, window, m.StatusRadiusPx, pen);

            DrawLed(g, m, state);
            DrawStatusText(g, m, fonts, state);
        }

        private static void DrawLed(Graphics g, AluMetrics m, AluPanelState state)
        {
            RectangleF bezel = m.LedBezel;
            RectangleF lens = m.LedLens;

            Color core, mid, edge;
            Color glow;
            if (state == AluPanelState.Locked)
            {
                core = AluPalette.LedLensRedCore;
                mid = AluPalette.LedLensRedMid;
                edge = AluPalette.LedLensRedEdge;
                glow = AluPalette.LedLensRedMid;
            }
            else if (state == AluPanelState.Unavailable)
            {
                core = AluPalette.LedLensAmberCore;
                mid = AluPalette.LedLensAmberMid;
                edge = AluPalette.LedLensAmberEdge;
                glow = AluPalette.LedLensAmberMid;
            }
            else
            {
                core = AluPalette.LedLensGreenCore;
                mid = AluPalette.LedLensGreenMid;
                edge = AluPalette.LedLensGreenEdge;
                glow = AluPalette.LedLensGreenMid;
            }

            // 灯座下沿的一道白色高光
            using (SolidBrush highlightBrush = new SolidBrush(AluPalette.Alpha("#FFFFFF", 0.22)))
                g.FillRectangle(highlightBrush, bezel.X, bezel.Bottom, bezel.Width, 1f * m.Scale);

            AluSurface.FillVerticalGradient(
                g, bezel,
                new Color[] { AluPalette.LedBezelTop, AluPalette.LedBezelBottom },
                new float[] { 0f, 1f });
            AluSurface.DrawInnerShadowTop(g, bezel, bezel.Width / 2f, AluPalette.Alpha("#000000", 0.92), 3f * m.Scale);

            // 灯罩外的辉光
            AluSurface.DrawGlow(g, lens, glow, 0.55, 1.6f);

            // 灯罩本体
            AluSurface.FillRadialGradient(
                g, lens, new PointF(0.5f, 0.65f),
                new Color[] { core, mid, edge },
                new float[] { 0f, 0.45f, 1f });

            // 左上角的高光点
            float hw = 5f * m.Scale;
            float hh = 3f * m.Scale;
            RectangleF spec = new RectangleF(lens.Left + 2.5f * m.Scale, lens.Top + 1.5f * m.Scale, hw, hh);
            using (SolidBrush specBrush = new SolidBrush(AluPalette.LedHighlight))
                AluSurface.FillRounded(g, spec, hh / 2f, specBrush);
        }

        private static void DrawStatusText(Graphics g, AluMetrics m, AluFonts fonts, AluPanelState state)
        {
            string cn;
            string en;
            Color enColor;

            if (state == AluPanelState.Locked)
            {
                cn = "已锁定";
                en = "LOCKED";
                enColor = AluPalette.StatusEnLocked;
            }
            else if (state == AluPanelState.Unavailable)
            {
                cn = "不可用";
                en = "HOOK FAILED";
                enColor = AluPalette.StatusEnUnavailable;
            }
            else
            {
                cn = "未锁定";
                en = "UNLOCKED";
                enColor = AluPalette.StatusEnUnlocked;
            }

            AluText.Draw(g, cn, fonts.StatusCn, AluPalette.StatusText, m.StatusTextCn,
                AluTextAlign.Left, 0f, Color.Transparent, 0f);

            AluText.Draw(g, en, fonts.StatusEn, enColor, m.StatusTextEn,
                AluTextAlign.Left, 1.33f, Color.Transparent, 0f);
        }
    }
}
