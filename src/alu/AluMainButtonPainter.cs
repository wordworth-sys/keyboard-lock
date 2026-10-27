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
        // PathGradientBrush 外圈颜色（全透明），两种状态各一份，避免每帧分配。
        private static readonly Color[] NormalHighlightOuter = { Color.FromArgb(0, 255, 255, 255) };
        private static readonly Color[] LockedHighlightOuter =
        {
            Color.FromArgb(0, AluPalette.MainKeyLockedGlow.R, AluPalette.MainKeyLockedGlow.G, AluPalette.MainKeyLockedGlow.B)
        };

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
            Color shadowColor = locked ? AluPalette.MainKeyLockedTextShadow : AluPalette.MainKeyTextShadow;

            AluText.Draw(g, label, fonts.MainLabel, textColor, button, AluTextAlign.Center, 1.28f, shadowColor, 1f);
        }

        private static void DrawSlot(Graphics g, AluMetrics m)
        {
            RectangleF slot = m.MainSlot;

            AluSurface.FillVerticalGradient(g, slot, AluPalette.MainSlotStops, AluPalette.TwoStopPositions);

            AluSurface.DrawInnerShadowTop(g, slot, m.MainSlotRadiusPx, AluPalette.ShadowMedium, 4f * m.Scale);

            AluSurface.DrawTopBand(g, slot, m.MainSlotRadiusPx, AluPalette.MainSlotBorderTop, 1f * m.Scale, 1f * m.Scale);
            AluSurface.DrawBottomBand(g, slot, m.MainSlotRadiusPx, AluPalette.MainSlotBorderBottom, 1.5f * m.Scale, 1f * m.Scale);
        }

        private static void DrawNormalFace(Graphics g, AluMetrics m, RectangleF button, float radius)
        {
            AluSurface.FillVerticalGradient(g, button, AluPalette.MainKeyStops, AluPalette.MainKeyPositions);

            // 顶部的一团白色高光（CSS: radial-gradient(80% 100% at 50% 0%, ...)）
            DrawTopHighlight(g, button, Color.White, NormalHighlightOuter, 0.8f);

            AluSurface.DrawInnerTopLight(g, button, radius, AluPalette.MainKeyTopLight, 1.5f * m.Scale);
            AluSurface.DrawInnerBottomEdge(g, button, radius, AluPalette.MainKeyInset, 3.5f * m.Scale);

            using (Pen pen = new Pen(AluPalette.MainKeyBorder, 1f))
                AluSurface.DrawRounded(g, button, radius, pen);
        }

        private static void DrawLockedFace(Graphics g, AluMetrics m, RectangleF button, float radius)
        {
            AluSurface.FillVerticalGradient(g, button, AluPalette.MainKeyLockedStops, AluPalette.MainKeyLockedPositions);

            // 锁定态是一层淡粉的高光，不是白光（CSS: rgba(255,185,165,.5)）
            DrawTopHighlight(g, button, AluPalette.MainKeyLockedGlow, LockedHighlightOuter, 0.9f);

            AluSurface.DrawInnerTopLight(g, button, radius, AluPalette.MainKeyLockedTopLight, 1.5f * m.Scale);
            AluSurface.DrawInnerBottomEdge(g, button, radius, AluPalette.MainKeyLockedInset, 3.5f * m.Scale);

            using (Pen pen = new Pen(AluPalette.MainKeyLockedBorder, 1f))
                AluSurface.DrawRounded(g, button, radius, pen);
        }

        /// <summary>按键顶部的椭圆高光（CSS radial-gradient 的近似）。</summary>
        private static void DrawTopHighlight(Graphics g, RectangleF button, Color center, Color[] surround, float radiusFactor)
        {
            float cx = button.Left + button.Width / 2f;
            float cy = button.Top;
            float rx = button.Width * radiusFactor;
            float ry = button.Height;
            RectangleF ellipse = new RectangleF(cx - rx, cy - ry, rx * 2f, ry * 2f);

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(ellipse);
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = new PointF(cx, cy);
                    brush.CenterColor = center;
                    brush.SurroundColors = surround;
                    g.FillEllipse(brush, ellipse);
                }
            }
        }
    }
}
