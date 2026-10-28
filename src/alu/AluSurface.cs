using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 绘制底座：圆角路径、多段渐变、径向高光、内斜面、外投影、机械条纹。
    /// 所有绘制模块共用这一套图元，保证整个面板的质感一致。
    /// </summary>
    internal static class AluSurface
    {
        /// <summary>生成圆角矩形路径。</summary>
        public static GraphicsPath RoundedPath(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (r.Width <= 0f || r.Height <= 0f)
                return path;

            float d = radius * 2f;
            if (d <= 0f || d > Math.Min(r.Width, r.Height))
            {
                path.AddRectangle(r);
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 180f, 90f);
            path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        public static void FillRounded(Graphics g, RectangleF r, float radius, Brush brush)
        {
            using (GraphicsPath path = RoundedPath(r, radius))
                g.FillPath(brush, path);
        }

        public static void DrawRounded(Graphics g, RectangleF r, float radius, Pen pen)
        {
            using (GraphicsPath path = RoundedPath(r, radius))
                g.DrawPath(pen, path);
        }

        /// <summary>把后续绘制限制在圆角区域内（与现有裁剪区求交，绝不冲掉局部重绘的 clip），返回用于还原的 GraphicsState。</summary>
        public static GraphicsState ClipRounded(Graphics g, RectangleF r, float radius)
        {
            GraphicsState state = g.Save();
            using (GraphicsPath path = RoundedPath(r, radius))
                g.SetClip(path, CombineMode.Intersect);
            return state;
        }

        /// <summary>竖直多段渐变（单把画刷 ColorBlend 插值，避免多段重叠接缝与重复创建画刷）。</summary>
        public static void FillVerticalGradient(Graphics g, RectangleF r, Color[] stops, float[] positions)
        {
            FillGradient(g, r, stops, positions, LinearGradientMode.Vertical);
        }

        /// <summary>水平多段渐变（左右边缘的高光 / 暗边）。</summary>
        public static void FillHorizontalGradient(Graphics g, RectangleF r, Color[] stops, float[] positions)
        {
            FillGradient(g, r, stops, positions, LinearGradientMode.Horizontal);
        }

        private static void FillGradient(Graphics g, RectangleF r, Color[] stops, float[] positions, LinearGradientMode mode)
        {
            if (r.Width <= 0f || r.Height <= 0f || stops.Length == 0)
                return;

            if (stops.Length == 1)
            {
                using (SolidBrush b = new SolidBrush(stops[0]))
                    g.FillRectangle(b, r);
                return;
            }

            using (LinearGradientBrush brush = CreateGradientBrush(r, stops, positions, mode))
                g.FillRectangle(brush, r);
        }

        private static LinearGradientBrush CreateGradientBrush(RectangleF r, Color[] stops, float[] positions, LinearGradientMode mode)
        {
            LinearGradientBrush brush = new LinearGradientBrush(r, stops[0], stops[stops.Length - 1], mode);
            brush.WrapMode = WrapMode.TileFlipXY;
            if (stops.Length > 2)
            {
                ColorBlend blend = new ColorBlend(stops.Length);
                blend.Colors = stops;
                blend.Positions = positions;
                brush.InterpolationColors = blend;
            }
            return brush;
        }

        /// <summary>
        /// 径向渐变：按半径分段由外向内叠画（LED 灯罩用）。
        /// stops / positions 与 CSS radial-gradient 的写法一致。
        /// </summary>
        public static void FillRadialGradient(Graphics g, RectangleF r, PointF focus, Color[] stops, float[] positions)
        {
            const int Steps = 24;
            float cx = r.Left + r.Width * focus.X;
            float cy = r.Top + r.Height * focus.Y;
            float rx = r.Width;
            float ry = r.Height;
            if (rx <= 0f || ry <= 0f)
                return;

            // 由外圈画向圆心：内圈覆盖外圈，最终圆心为 stops[0]。一把画刷改色复用，不每步新建。
            using (SolidBrush brush = new SolidBrush(Color.Black))
            {
                for (int i = 0; i < Steps; i++)
                {
                    float t = 1f - (float)(i + 0.5) / Steps;          // 1 = 外缘，0 = 圆心
                    brush.Color = Sample(stops, positions, t);
                    g.FillEllipse(brush, cx - rx * t, cy - ry * t, rx * t * 2f, ry * t * 2f);
                }
            }
        }

        /// <summary>按位置采样一段渐变（positions 为 0~1 的相对位置）。</summary>
        public static Color Sample(Color[] stops, float[] positions, float t)
        {
            if (stops.Length == 0)
                return Color.Transparent;
            if (t <= positions[0])
                return stops[0];

            for (int i = 0; i < stops.Length - 1; i++)
            {
                if (t <= positions[i + 1])
                {
                    float span = positions[i + 1] - positions[i];
                    float k = span <= 0f ? 0f : (t - positions[i]) / span;
                    return Blend(stops[i], stops[i + 1], k);
                }
            }

            return stops[stops.Length - 1];
        }

        public static Color Blend(Color a, Color b, float k)
        {
            k = Math.Max(0f, Math.Min(1f, k));
            return Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * k),
                (int)Math.Round(a.R + (b.R - a.R) * k),
                (int)Math.Round(a.G + (b.G - a.G) * k),
                (int)Math.Round(a.B + (b.B - a.B) * k));
        }

        /// <summary>
        /// 柔和外发光：以圆心向外扩散，透明度按平方衰减（CSS box-shadow 0 0 Npx 的近似）。
        /// </summary>
        public static void DrawGlow(Graphics g, RectangleF lens, Color color, double maxAlpha, float spread)
        {
            const int Layers = 10;
            float cx = lens.Left + lens.Width / 2f;
            float cy = lens.Top + lens.Height / 2f;
            float baseR = Math.Max(lens.Width, lens.Height) / 2f;

            using (SolidBrush brush = new SolidBrush(Color.Black))
            {
                for (int k = Layers; k >= 1; k--)
                {
                    float t = (float)k / Layers;                       // 1 = 最外层
                    float radius = baseR * (1f + spread * t);
                    float target = (float)maxAlpha * (1f - t) * (1f - t);
                    if (target <= 0.002f)
                        continue;

                    // 每一层的 alpha 反推自「累计不透明度」，叠出接近二次衰减的光晕。
                    int layersBelow = Layers - k + 1;
                    double a = 1.0 - Math.Pow(1.0 - target, 1.0 / layersBelow);
                    int alpha = (int)Math.Round(a * 255.0);
                    if (alpha <= 0)
                        continue;

                    brush.Color = Color.FromArgb(alpha, color.R, color.G, color.B);
                    g.FillEllipse(brush, cx - radius, cy - radius, radius * 2f, radius * 2f);
                }
            }
        }

        /// <summary>内斜面：顶部一条高光线（沿圆角轮廓，只保留顶部一段）。</summary>
        public static void DrawInnerTopLight(Graphics g, RectangleF r, float radius, Color color, float thickness)
        {
            if (thickness <= 0f)
                return;

            GraphicsState state = g.Save();
            using (GraphicsPath path = RoundedPath(r, radius))
            {
                g.SetClip(path, CombineMode.Intersect);
                g.SetClip(new RectangleF(r.X, r.Y, r.Width, thickness), CombineMode.Intersect);
                using (Pen pen = new Pen(color, thickness))
                    g.DrawPath(pen, path);
            }
            g.Restore(state);
        }

        /// <summary>内斜面：底部一条压边线（沿圆角轮廓，只保留底部一段）。</summary>
        public static void DrawInnerBottomEdge(Graphics g, RectangleF r, float radius, Color color, float thickness)
        {
            if (thickness <= 0f)
                return;

            GraphicsState state = g.Save();
            using (GraphicsPath path = RoundedPath(r, radius))
            {
                g.SetClip(path, CombineMode.Intersect);
                g.SetClip(new RectangleF(r.X, r.Bottom - thickness, r.Width, thickness), CombineMode.Intersect);
                using (Pen pen = new Pen(color, thickness))
                    g.DrawPath(pen, path);
            }
            g.Restore(state);
        }

        /// <summary>柔和投影：向下偏移的模糊暗影（用逐层收缩的圆角叠加近似）。</summary>
        public static void DrawDropShadow(Graphics g, RectangleF r, float radius, Color color, float offsetY, float spread)
        {
            const int Layers = 5;
            using (SolidBrush brush = new SolidBrush(Color.Black))
            {
                for (int i = Layers; i >= 1; i--)
                {
                    float t = (float)i / Layers;                   // 1 = 最外层
                    RectangleF shadow = new RectangleF(r.X, r.Y + offsetY, r.Width, r.Height);
                    shadow.Inflate(spread * t, spread * t);
                    int alpha = (int)Math.Round(color.A * (1f - t) * 0.9f);
                    if (alpha <= 0)
                        continue;
                    brush.Color = Color.FromArgb(alpha, color.R, color.G, color.B);
                    FillRounded(g, shadow, radius + spread * t, brush);
                }
            }
        }

        /// <summary>圆角矩形顶部的水平压边（自动跟随圆角裁剪，不会溢出角外）。</summary>
        public static void DrawTopBand(Graphics g, RectangleF r, float radius, Color color, float thickness, float inset)
        {
            if (thickness <= 0f)
                return;

            GraphicsState state = g.Save();
            using (GraphicsPath path = RoundedPath(r, radius))
            {
                g.SetClip(path, CombineMode.Intersect);
                using (SolidBrush brush = new SolidBrush(color))
                    g.FillRectangle(brush, r.X + inset, r.Y, r.Width - inset * 2f, thickness);
            }
            g.Restore(state);
        }

        /// <summary>圆角矩形底部的水平压边（自动跟随圆角裁剪）。</summary>
        public static void DrawBottomBand(Graphics g, RectangleF r, float radius, Color color, float thickness, float inset)
        {
            if (thickness <= 0f)
                return;

            GraphicsState state = g.Save();
            using (GraphicsPath path = RoundedPath(r, radius))
            {
                g.SetClip(path, CombineMode.Intersect);
                using (SolidBrush brush = new SolidBrush(color))
                    g.FillRectangle(brush, r.X + inset, r.Bottom - thickness, r.Width - inset * 2f, thickness);
            }
            g.Restore(state);
        }

        /// <summary>内阴影：顶部往下的一段渐隐暗影（CSS inset 0 Npx Mpx rgba(0,0,0,a)）。</summary>
        public static void DrawInnerShadowTop(Graphics g, RectangleF r, float radius, Color color, float depth)
        {
            if (depth <= 0f)
                return;

            GraphicsState state = ClipRounded(g, r, radius);
            RectangleF band = new RectangleF(r.X, r.Y, r.Width, depth);
            using (LinearGradientBrush brush = new LinearGradientBrush(
                band, color, Color.FromArgb(0, color.R, color.G, color.B), LinearGradientMode.Vertical))
                g.FillRectangle(brush, band);
            g.Restore(state);
        }

        /// <summary>圆角区域内的竖直渐变填充（直接沿圆角路径抗锯齿填充，无直角外溢）。</summary>
        public static void FillRoundedVerticalGradient(Graphics g, RectangleF r, float radius, Color[] stops, float[] positions)
        {
            if (r.Width <= 0f || r.Height <= 0f || stops.Length == 0)
                return;

            if (stops.Length == 1)
            {
                using (SolidBrush b = new SolidBrush(stops[0]))
                    FillRounded(g, r, radius, b);
                return;
            }

            using (LinearGradientBrush brush = CreateGradientBrush(r, stops, positions, LinearGradientMode.Vertical))
                FillRounded(g, r, radius, brush);
        }

        /// <summary>竖直机械条纹（导轨拉丝）。</summary>
        public static void FillVerticalStripes(Graphics g, RectangleF r, Color light, Color dark, float period, float duty)
        {
            if (r.Width <= 0f || r.Height <= 0f || period <= 0f)
                return;

            // 两把画刷贯穿整个循环；此前每条纹都新建两把，一帧下来近百个 GDI 对象。
            using (SolidBrush lightBrush = new SolidBrush(light))
            using (SolidBrush darkBrush = new SolidBrush(dark))
            {
                for (float x = r.Left; x < r.Right; x += period)
                {
                    g.FillRectangle(lightBrush, x, r.Top, duty, r.Height);
                    g.FillRectangle(darkBrush, x + duty, r.Top, period - duty, r.Height);
                }
            }
        }
    }
}
