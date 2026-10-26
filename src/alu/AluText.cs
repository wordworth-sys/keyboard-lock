using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    /// </summary>
    internal static class AluText
    {
        /// <summary>测量一段文字的排版宽度（含字间距）。</summary>
        public static float MeasureWidth(Graphics g, string text, Font font, float letterSpacing)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;

            float total = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                total += MeasureChar(g, text[i].ToString(), font);
                if (i < text.Length - 1)
                    total += letterSpacing;
            }
            return total;
        }

        /// <summary>
        /// 在矩形里排版一段文字：垂直居中，水平按 align 对齐，可选 1px 投影。
        /// </summary>
        public static void Draw(Graphics g, string text, Font font, Color color, RectangleF rect,
            AluTextAlign align, float letterSpacing, Color shadowColor, float shadowOffsetY)
        {
            if (string.IsNullOrEmpty(text) || font == null)
                return;

            if (shadowColor.A > 0)
                DrawRun(g, text, font, shadowColor, rect, align, letterSpacing, shadowOffsetY);

            DrawRun(g, text, font, color, rect, align, letterSpacing, 0f);
        }

        private static void DrawRun(Graphics g, string text, Font font, Color color, RectangleF rect,
            AluTextAlign align, float letterSpacing, float offsetY)
        {
            TextRenderingHint previousHint = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat(StringFormat.GenericTypographic))
            {
                format.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
                format.LineAlignment = StringAlignment.Center;

                float width = MeasureWidth(g, text, font, letterSpacing);
                float x;
                if (align == AluTextAlign.Center)
                    x = rect.Left + (rect.Width - width) / 2f;
                else if (align == AluTextAlign.Right)
                    x = rect.Right - width;
                else
                    x = rect.Left;

                float top = rect.Top + offsetY;
                for (int i = 0; i < text.Length; i++)
                {
                    string ch = text[i].ToString();
                    float charWidth = MeasureChar(g, ch, font);
                    g.DrawString(ch, font, brush, new RectangleF(x, top, charWidth + 1f, rect.Height), format);
                    x += charWidth + letterSpacing;
                }
            }

            g.TextRenderingHint = previousHint;
        }

        private static float MeasureChar(Graphics g, string ch, Font font)
        {
            SizeF size = g.MeasureString(ch, font, int.MaxValue, StringFormat.GenericTypographic);
            return size.Width;
        }
    }
}
