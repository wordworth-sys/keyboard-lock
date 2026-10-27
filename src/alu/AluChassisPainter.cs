using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 铝制机身：底色渐变、顶部高光、左右边缘反光、内斜面、标题栏底色与外边框。
    /// 这一层只画「静态」结构，交互部件交给各自的 Painter。
    /// </summary>
    internal static class AluChassisPainter
    {
        // PathGradientBrush 的外圈颜色：固定不变，避免每帧 new 一个单元素数组。
        private static readonly Color[] TopHighlightOuterSoft = { Color.FromArgb(0, 255, 255, 255) };
        private static readonly Color[] TopHighlightOuterCore = { Color.FromArgb(92, 255, 255, 255) };

        public static void Draw(Graphics g, AluMetrics m)
        {
            RectangleF chassis = m.Chassis;
            RectangleF content = m.ChassisContent;
            float radius = m.ChassisRadiusPx;
            float s = m.Scale;

            GraphicsState clip = AluSurface.ClipRounded(g, chassis, radius);

            // 1. 机身底色：竖直多段渐变
            AluSurface.FillVerticalGradient(g, chassis, AluPalette.ChassisStops, AluPalette.ChassisPositions);

            // 2. 顶部径向高光（CSS: radial-gradient(125% 65% at 50% -6%, ...)）
            DrawTopHighlight(g, chassis);

            // 3. 左右边缘反光（CSS: linear-gradient(90deg, ...)）
            AluSurface.FillHorizontalGradient(g, chassis, AluPalette.ChassisEdgeStops, AluPalette.ChassisEdgePositions);

            // 4. 内斜面：顶部高光、左右细白线、底部压边
            AluSurface.DrawInnerTopLight(g, content, radius - s, AluPalette.ChassisBevelTop, 2f * s);

            using (SolidBrush sideBrush = new SolidBrush(AluPalette.ChassisBevelSide))
            {
                g.FillRectangle(sideBrush, content.X, content.Y, 1.5f * s, content.Height);
                g.FillRectangle(sideBrush, content.Right - 1.5f * s, content.Y, 1.5f * s, content.Height);
            }

            using (SolidBrush bottomBrush = new SolidBrush(AluPalette.ChassisBottomRidge))
                g.FillRectangle(bottomBrush, content.X + 2f * s, content.Bottom - 3f * s, content.Width - 4f * s, 3f * s);

            using (SolidBrush lineBrush = new SolidBrush(AluPalette.ChassisBevelBottomLine))
                g.FillRectangle(lineBrush, content.X + 2f * s, content.Bottom - 4f * s, content.Width - 4f * s, 1f * s);

            // 5. 标题栏底色
            DrawTitleBar(g, m);

            g.Restore(clip);

            // 6. 外边框
            RectangleF border = new RectangleF(chassis.X + 0.5f, chassis.Y + 0.5f, chassis.Width - 1f, chassis.Height - 1f);
            using (Pen pen = new Pen(AluPalette.ChassisBorder, 1f))
                AluSurface.DrawRounded(g, border, radius, pen);
        }

        /// <summary>
        /// 凹槽模块的外框：滑块槽与状态窗共用的深色下沉面板。
        /// </summary>
        public static void DrawRecessedModule(Graphics g, AluMetrics m)
        {
            RectangleF module = m.Module;
            float s = m.Scale;

            AluSurface.FillVerticalGradient(g, module, AluPalette.ModuleStops, AluPalette.TwoStopPositions);

            AluSurface.DrawInnerShadowTop(g, module, m.ModuleRadiusPx, AluPalette.ShadowStrong, 9f * s);

            AluSurface.DrawTopBand(g, module, m.ModuleRadiusPx, AluPalette.ModuleBorderTop, 1.5f * s, 1f * s);
            AluSurface.DrawBottomBand(g, module, m.ModuleRadiusPx, AluPalette.ModuleBorderBottom, 1.5f * s, 1f * s);
        }

        private static void DrawTopHighlight(Graphics g, RectangleF chassis)
        {
            float cx = chassis.Left + chassis.Width * 0.5f;
            float cy = chassis.Top - chassis.Height * 0.06f;
            float rx = chassis.Width * 1.25f;
            float ry = chassis.Height * 0.65f;

            // 68% 圈：中心 alpha .36 → 外缘 0
            DrawRadialAlpha(g, cx, cy, rx * 0.68f, ry * 0.68f, 92, TopHighlightOuterSoft);
            // 38% 圈：中心 alpha .95 → 外缘 .36
            DrawRadialAlpha(g, cx, cy, rx * 0.38f, ry * 0.38f, 242, TopHighlightOuterCore);
        }

        private static void DrawRadialAlpha(Graphics g, float cx, float cy, float rx, float ry, int centerAlpha, Color[] surround)
        {
            if (rx <= 0f || ry <= 0f)
                return;

            RectangleF ellipse = new RectangleF(cx - rx, cy - ry, rx * 2f, ry * 2f);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(ellipse);
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterPoint = new PointF(cx, cy);
                    brush.CenterColor = Color.FromArgb(centerAlpha, 255, 255, 255);
                    brush.SurroundColors = surround;
                    g.FillEllipse(brush, ellipse);
                }
            }
        }

        private static void DrawTitleBar(Graphics g, AluMetrics m)
        {
            RectangleF bar = m.TitleBar;
            float s = m.Scale;

            AluSurface.FillVerticalGradient(g, bar, AluPalette.TitleBarStops, AluPalette.TitleBarPositions);

            AluSurface.DrawInnerTopLight(g, bar, m.ChassisRadiusPx - s, AluPalette.TitleBarBevel, 1.5f * s);

            // 底部分隔线 + 线下的一道白色高光
            using (SolidBrush borderBrush = new SolidBrush(AluPalette.TitleBarBorder))
                g.FillRectangle(borderBrush, bar.X, bar.Bottom - 1f * s, bar.Width, 1f * s);

            using (SolidBrush shadowBrush = new SolidBrush(AluPalette.TitleBarUnderline))
                g.FillRectangle(shadowBrush, bar.X, bar.Bottom, bar.Width, 1f * s);
        }
    }
}
