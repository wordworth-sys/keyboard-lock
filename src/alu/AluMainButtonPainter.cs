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

            DrawFace(g, m, button, radius, locked);

            string label = disabled ? "键盘钩子安装失败" : (locked ? "解锁键盘" : "锁定键盘");
            Color textColor = disabled ? AluPalette.MainKeyDisabledText : (locked ? Color.White : AluPalette.MainKeyText);
            Color shadowColor = locked ? AluPalette.MainKeyLockedTextShadow : AluPalette.MainKeyTextShadow;

            AluText.Draw(g, label, fonts.MainLabel, textColor, button, AluTextAlign.Center, 1.28f * m.Scale, shadowColor, 1f * m.Scale);
        }

        private static void DrawSlot(Graphics g, AluMetrics m)
        {
            RectangleF slot = m.MainSlot;

            AluSurface.FillRoundedVerticalGradient(g, slot, m.MainSlotRadiusPx, AluPalette.MainSlotStops, AluPalette.TwoStopPositions);

            AluSurface.DrawInnerShadowTop(g, slot, m.MainSlotRadiusPx, AluPalette.ShadowMedium, 4f * m.Scale);

            AluSurface.DrawTopBand(g, slot, m.MainSlotRadiusPx, AluPalette.MainSlotBorderTop, 1f * m.Scale, 1f * m.Scale);
            AluSurface.DrawBottomBand(g, slot, m.MainSlotRadiusPx, AluPalette.MainSlotBorderBottom, 1.5f * m.Scale, 1f * m.Scale);
        }

        private static void DrawFace(Graphics g, AluMetrics m, RectangleF button, float radius, bool locked)
        {
            Color[] stops = locked ? AluPalette.MainKeyLockedStops : AluPalette.MainKeyStops;
            float[] positions = locked ? AluPalette.MainKeyLockedPositions : AluPalette.MainKeyPositions;
            Color glowCenter = locked ? AluPalette.MainKeyLockedGlow : Color.White;
            Color[] glowOuter = locked ? LockedHighlightOuter : NormalHighlightOuter;
            float glowRadius = locked ? 0.9f : 0.8f;
            Color topLight = locked ? AluPalette.MainKeyLockedTopLight : AluPalette.MainKeyTopLight;
            Color inset = locked ? AluPalette.MainKeyLockedInset : AluPalette.MainKeyInset;
            Color border = locked ? AluPalette.MainKeyLockedBorder : AluPalette.MainKeyBorder;

            AluSurface.FillRoundedVerticalGradient(g, button, radius, stops, positions);

            // 顶部高光椭圆上半截高出按钮顶边，必须裁在按钮圆角内，避免溢出到机身与凹槽模块。
            GraphicsState clip = AluSurface.ClipRounded(g, button, radius);
            DrawTopHighlight(g, button, glowCenter, glowOuter, glowRadius);
            g.Restore(clip);

            AluSurface.DrawInnerTopLight(g, button, radius, topLight, 1.5f * m.Scale);
            AluSurface.DrawInnerBottomEdge(g, button, radius, inset, 3.5f * m.Scale);

            using (Pen pen = new Pen(border, 1f))
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
