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

        /// <summary>把后续绘制限制在圆角区域内，返回用于还原的 GraphicsState。</summary>
        public static GraphicsState ClipRounded(Graphics g, RectangleF r, float radius)
        {
            GraphicsState state = g.Save();
            using (GraphicsPath path = RoundedPath(r, radius))
                g.SetClip(path);
            return state;
        }

        /// <summary>竖直多段渐变（各段之间重叠 1px，避免出现接缝）。</summary>
        public static void FillVerticalGradient(Graphics g, RectangleF r, Color[] stops, float[] positions)
        {
            if (r.Width <= 0f || r.Height <= 0f)
                return;

            if (stops.Length == 1)
            {
                using (SolidBrush b = new SolidBrush(stops[0]))
                    g.FillRectangle(b, r);
                return;
            }

            for (int i = 0; i < stops.Length - 1; i++)
            {
                float top = r.Top + r.Height * positions[i];
                float bottom = r.Top + r.Height * positions[i + 1];
                RectangleF band = new RectangleF(r.X, top, r.Width, Math.Max(1f, bottom - top + 1f));
                using (LinearGradientBrush brush = new LinearGradientBrush(band, stops[i], stops[i + 1], LinearGradientMode.Vertical))
                    g.FillRectangle(brush, band);
            }
        }

        /// <summary>水平多段渐变（左右边缘的高光 / 暗边）。</summary>
        public static void FillHorizontalGradient(Graphics g, RectangleF r, Color[] stops, float[] positions)
        {
            if (r.Width <= 0f || r.Height <= 0f)
                return;

            if (stops.Length == 1)
            {
                using (SolidBrush b = new SolidBrush(stops[0]))
                    g.FillRectangle(b, r);
                return;
            }

            for (int i = 0; i < stops.Length - 1; i++)
            {
                float left = r.Left + r.Width * positions[i];
                float right = r.Left + r.Width * positions[i + 1];
                RectangleF band = new RectangleF(left, r.Top, Math.Max(1f, right - left + 1f), r.Height);
                using (LinearGradientBrush brush = new LinearGradientBrush(band, stops[i], stops[i + 1], LinearGradientMode.Horizontal))
                    g.FillRectangle(brush, band);
            }
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

            // 由外圈画向圆心：内圈覆盖外圈，最终圆心为 stops[0]。
            for (int i = 0; i < Steps; i++)
            {
                float t = 1f - (float)(i + 0.5) / Steps;          // 1 = 外缘，0 = 圆心
                Color c = Sample(stops, positions, t);
                if (rx <= 0f || ry <= 0f)
                    continue;
                RectangleF band = new RectangleF(cx - rx * t, cy - ry * t, rx * t * 2f, ry * t * 2f);
                using (SolidBrush brush = new SolidBrush(c))
                    g.FillEllipse(brush, band);
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

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, color.R, color.G, color.B)))
                    g.FillEllipse(brush, cx - radius, cy - radius, radius * 2f, radius * 2f);
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
                g.SetClip(path);
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
                g.SetClip(path);
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
            for (int i = Layers; i >= 1; i--)
            {
                float t = (float)i / Layers;                   // 1 = 最外层
                RectangleF shadow = new RectangleF(r.X, r.Y + offsetY, r.Width, r.Height);
                shadow.Inflate(spread * t, spread * t);
                int alpha = (int)Math.Round(color.A * (1f - t) * 0.9f);
                if (alpha <= 0)
                    continue;
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(alpha, color.R, color.G, color.B)))
                    FillRounded(g, shadow, radius + spread * t, brush);
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
                g.SetClip(path);
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
                g.SetClip(path);
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

        /// <summary>圆角区域内的竖直渐变填充（圆角由裁剪保证）。</summary>
        public static void FillRoundedVerticalGradient(Graphics g, RectangleF r, float radius, Color[] stops, float[] positions)
        {
            if (r.Width <= 0f || r.Height <= 0f)
                return;

            GraphicsState state = ClipRounded(g, r, radius);
            FillVerticalGradient(g, r, stops, positions);
            g.Restore(state);
        }

        /// <summary>竖直机械条纹（导轨拉丝）。</summary>
        public static void FillVerticalStripes(Graphics g, RectangleF r, Color light, Color dark, float period, float duty)
        {
            if (r.Width <= 0f || r.Height <= 0f || period <= 0f)
                return;

            float x = r.Left;
            while (x < r.Right)
            {
                RectangleF lightBand = new RectangleF(x, r.Top, duty, r.Height);
                using (SolidBrush brush = new SolidBrush(light))
                    g.FillRectangle(brush, lightBand);

                RectangleF darkBand = new RectangleF(x + duty, r.Top, period - duty, r.Height);
                using (SolidBrush brush = new SolidBrush(dark))
                    g.FillRectangle(brush, darkBand);

                x += period;
            }
        }
    }
}
