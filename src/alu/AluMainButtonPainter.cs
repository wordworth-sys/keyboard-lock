using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 主操作键：一颗沉在铝槽里的大键帽，锁定后整颗转为警示红。
    /// </summary>
    internal static class AluMainButtonPainter
    {
        public static void Draw(Graphics g, AluMetrics m, AluFonts fonts, AluPanelState state, bool pressed)
        {
            DrawSlot(g, m);

            bool locked = state == AluPanelState.Locked;
            bool disabled = state == AluPanelState.Unavailable;

            RectangleF button = m.MainButton;
            if (pressed && !disabled)
                button = new RectangleF(button.X, button.Y + 2f * m.Scale, button.Width, button.Height);

            float radius = m.MainKeyRadiusPx;

            if (!pressed)
                AluSurface.DrawDropShadow(g, button, radius, AluPalette.ShadowSoft, 3f * m.Scale, 5f * m.Scale);

            if (locked)
                DrawLockedFace(g, m, button, radius);
            else
                DrawNormalFace(g, m, button, radius);

            string label = disabled ? "键盘钩子安装失败" : (locked ? "解锁键盘" : "锁定键盘");
            Color textColor = disabled ? AluPalette.MainKeyDisabledText : (locked ? Color.White : AluPalette.MainKeyText);
            Color shadowColor = locked ? AluPalette.Alpha("#5A0F05", 0.80) : AluPalette.MainKeyTextShadow;

            AluText.Draw(g, label, fonts.MainLabel, textColor, button, AluTextAlign.Center, 1.28f, shadowColor, 1f);
        }

        private static void DrawSlot(Graphics g, AluMetrics m)
        {
            RectangleF slot = m.MainSlot;

            AluSurface.FillVerticalGradient(
                g, slot,
                new Color[] { AluPalette.MainSlotTop, AluPalette.MainSlotBottom },
                new float[] { 0f, 1f });

            AluSurface.DrawInnerShadowTop(g, slot, m.MainSlotRadiusPx, AluPalette.Alpha("#000000", 0.52), 4f * m.Scale);

            AluSurface.DrawTopBand(g, slot, m.MainSlotRadiusPx, AluPalette.MainSlotBorderTop, 1f * m.Scale, 1f * m.Scale);
            AluSurface.DrawBottomBand(g, slot, m.MainSlotRadiusPx, AluPalette.MainSlotBorderBottom, 1.5f * m.Scale, 1f * m.Scale);
        }

        private static void DrawNormalFace(Graphics g, AluMetrics m, RectangleF button, float radius)
        {
            AluSurface.FillVerticalGradient(
                g, button,
                new Color[] { AluPalette.MainKeyTop, AluPalette.MainKeyMid, AluPalette.MainKeyBottom },
                new float[] { 0f, 0.55f, 1f });

            // 顶部的一团白色高光（CSS: radial-gradient(80% 100% at 50% 0%, ...)）
            DrawTopHighlight(g, button, Color.FromArgb(255, 255, 255, 255), 0.8f);

            AluSurface.DrawInnerTopLight(g, button, radius, AluPalette.Alpha("#FFFFFF", 0.95), 1.5f * m.Scale);
            AluSurface.DrawInnerBottomEdge(g, button, radius, AluPalette.MainKeyInset, 3.5f * m.Scale);

            using (Pen pen = new Pen(AluPalette.MainKeyBorder, 1f))
                AluSurface.DrawRounded(g, button, radius, pen);
        }

        private static void DrawLockedFace(Graphics g, AluMetrics m, RectangleF button, float radius)
        {
            AluSurface.FillVerticalGradient(
                g, button,
                new Color[] { AluPalette.MainKeyLockedTop, AluPalette.MainKeyLockedMid, AluPalette.MainKeyLockedBottom },
                new float[] { 0f, 0.58f, 1f });

            // 锁定态是一层淡粉的高光，不是白光（CSS: rgba(255,185,165,.5)）
            DrawTopHighlight(g, button, AluPalette.MainKeyLockedGlow, 0.9f);

            AluSurface.DrawInnerTopLight(g, button, radius, AluPalette.Alpha("#FFDCDA", 0.70), 1.5f * m.Scale);
            AluSurface.DrawInnerBottomEdge(g, button, radius, AluPalette.MainKeyLockedInset, 3.5f * m.Scale);

            using (Pen pen = new Pen(AluPalette.MainKeyLockedBorder, 1f))
                AluSurface.DrawRounded(g, button, radius, pen);
        }

        /// <summary>按键顶部的椭圆高光（CSS radial-gradient 的近似）。</summary>
        private static void DrawTopHighlight(Graphics g, RectangleF button, Color color, float radiusFactor)
        {
            float cx = button.Left + button.Width / 2f;
            float cy = button.Top;
            float rx = button.Width * radiusFactor;
            float ry = button.Height;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(new RectangleF(cx - rx, cy - ry, rx * 2f, ry * 2f));
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = new PointF(cx, cy);
                    brush.CenterColor = color;
                    brush.SurroundColors = new Color[] { Color.FromArgb(0, color.R, color.G, color.B) };
                    g.FillEllipse(brush, new RectangleF(cx - rx, cy - ry, rx * 2f, ry * 2f));
                }
            }
        }
    }
}
