using System;
using System.Drawing;
using System.Drawing.Text;

namespace KeyboardLock.Alu
{
    internal enum AluTextAlign
    {
        Left,
        Center,
        Right
    }

    /// <summary>
    /// 文字绘制：GDI+ 逐字排版，支持 CSS 的 letter-spacing 与 text-shadow。
    /// 不用 TextRenderer 是因为它无法设置字间距，状态窗的英文小标签需要字间距。
    ///
    /// 每个字形只测量一次：测量结果放在线程内的暂存区里，阴影层与正文层共用，
    /// 不再像之前那样"测一遍宽度、画阴影再逐字测、画正文又逐字测"。
    /// </summary>
    internal static class AluText
    {
        // 面板上最长的文字不到 16 个字；只在 UI 线程上绘制，暂存区按线程隔离即可。
        private const int ScratchCapacity = 64;

        [ThreadStatic] private static string[] glyphScratch;
        [ThreadStatic] private static float[] widthScratch;
        [ThreadStatic] private static StringFormat formatScratch;

        /// <summary>测量一段文字的排版宽度（含字间距）。</summary>
        public static float MeasureWidth(Graphics g, string text, Font font, float letterSpacing)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;

            StringFormat format = Format(AluTextAlign.Left);
            if (letterSpacing == 0f)
                return g.MeasureString(text, font, int.MaxValue, format).Width;

            string[] glyphs;
            float[] widths;
            return Measure(g, text, font, letterSpacing, format, out glyphs, out widths);
        }

        /// <summary>
        /// 在矩形里排版一段文字：垂直居中，水平按 align 对齐，可选投影。
        /// 字间距为 0 时直接整串绘制（零切分分配、零逐字测量）；非零字间距时才走逐字排版。
        /// </summary>
        public static void Draw(Graphics g, string text, Font font, Color color, RectangleF rect,
            AluTextAlign align, float letterSpacing, Color shadowColor, float shadowOffsetY)
        {
            if (string.IsNullOrEmpty(text) || font == null)
                return;

            TextRenderingHint previousHint = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            if (letterSpacing == 0f)
            {
                StringFormat wholeFormat = Format(align);
                using (SolidBrush brush = new SolidBrush(shadowColor.A > 0 ? shadowColor : color))
                {
                    if (shadowColor.A > 0)
                    {
                        RectangleF shadowRect = new RectangleF(rect.X, rect.Y + shadowOffsetY, rect.Width, rect.Height);
                        g.DrawString(text, font, brush, shadowRect, wholeFormat);
                        brush.Color = color;
                    }
                    g.DrawString(text, font, brush, rect, wholeFormat);
                }

                g.TextRenderingHint = previousHint;
                return;
            }

            StringFormat glyphFormat = Format(AluTextAlign.Left);
            string[] glyphs;
            float[] widths;
            float width = Measure(g, text, font, letterSpacing, glyphFormat, out glyphs, out widths);

            float startX;
            if (align == AluTextAlign.Center)
                startX = rect.Left + (rect.Width - width) / 2f;
            else if (align == AluTextAlign.Right)
                startX = rect.Right - width;
            else
                startX = rect.Left;

            using (SolidBrush brush = new SolidBrush(shadowColor.A > 0 ? shadowColor : color))
            {
                if (shadowColor.A > 0)
                {
                    DrawRun(g, text.Length, glyphs, widths, font, brush, startX, rect.Top + shadowOffsetY, rect.Height, letterSpacing, glyphFormat);
                    brush.Color = color;
                }
                DrawRun(g, text.Length, glyphs, widths, font, brush, startX, rect.Top, rect.Height, letterSpacing, glyphFormat);
            }

            g.TextRenderingHint = previousHint;
        }

        private static void DrawRun(Graphics g, int count, string[] glyphs, float[] widths, Font font, Brush brush,
            float x, float top, float height, float letterSpacing, StringFormat format)
        {
            for (int i = 0; i < count; i++)
            {
                g.DrawString(glyphs[i], font, brush, new RectangleF(x, top, widths[i] + 1f, height), format);
                x += widths[i] + letterSpacing;
            }
        }

        /// <summary>逐字测量，结果写入暂存区并返回总宽（含字间距）。必须使用带 MeasureTrailingSpaces 的 format，否则空格宽度为 0。</summary>
        private static float Measure(Graphics g, string text, Font font, float letterSpacing, StringFormat format,
            out string[] glyphs, out float[] widths)
        {
            int n = text.Length;
            if (n <= ScratchCapacity)
            {
                if (glyphScratch == null)
                {
                    glyphScratch = new string[ScratchCapacity];
                    widthScratch = new float[ScratchCapacity];
                }
                glyphs = glyphScratch;
                widths = widthScratch;
            }
            else
            {
                glyphs = new string[n];
                widths = new float[n];
            }

            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                string ch = text[i].ToString();
                glyphs[i] = ch;
                widths[i] = g.MeasureString(ch, font, int.MaxValue, format).Width;
                total += widths[i];
            }
            if (n > 1)
                total += letterSpacing * (n - 1);
            return total;
        }

        private static StringFormat Format(AluTextAlign align)
        {
            if (formatScratch == null)
            {
                formatScratch = new StringFormat(StringFormat.GenericTypographic);
                formatScratch.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip;
                formatScratch.LineAlignment = StringAlignment.Center;
            }

            if (align == AluTextAlign.Center)
                formatScratch.Alignment = StringAlignment.Center;
            else if (align == AluTextAlign.Right)
                formatScratch.Alignment = StringAlignment.Far;
            else
                formatScratch.Alignment = StringAlignment.Near;

            return formatScratch;
        }
    }
}
