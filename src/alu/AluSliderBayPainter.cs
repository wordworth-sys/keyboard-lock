using System;
using System.Drawing;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 机械滑块槽：拉丝导轨、FREE / HOLD 刻度、可滑动的 LOCK 滑块。
    /// 滑块位置直接由锁定状态决定（未锁定在左、已锁定在右）。
    /// </summary>
    internal static class AluSliderBayPainter
    {
        public static void Draw(Graphics g, AluMetrics m, AluFonts fonts, AluPanelState state)
        {
            RectangleF bay = m.SliderBay;

            // 导轨底色 + 机械拉丝条纹
            AluSurface.FillVerticalGradient(g, bay, AluPalette.RailStops, AluPalette.TwoStopPositions);

            AluSurface.FillVerticalStripes(g, bay, AluPalette.RailStripeLight, AluPalette.RailStripeDark,
                4f * m.Scale, 2f * m.Scale);

            AluSurface.DrawInnerShadowTop(g, bay, m.BayRadiusPx, AluPalette.RailInnerShadow, 5f * m.Scale);

            // 刻度文字（滑块会盖住其中一侧）
            float labelY = m.Rail.Top + (m.Rail.Height - 12f * m.Scale) / 2f;
            RectangleF freeRect = new RectangleF(m.Rail.Left + 8f * m.Scale, labelY, 60f * m.Scale, 12f * m.Scale);
            RectangleF holdRect = new RectangleF(m.Rail.Right - 68f * m.Scale, labelY, 60f * m.Scale, 12f * m.Scale);

            AluText.Draw(g, "FREE", fonts.RailLabel, AluPalette.RailLabel, freeRect, AluTextAlign.Left, 0f, Color.Transparent, 0f);
            AluText.Draw(g, "HOLD", fonts.RailLabel, AluPalette.RailLabel, holdRect, AluTextAlign.Right, 0f, Color.Transparent, 0f);

            // 滑块
            RectangleF thumb = state == AluPanelState.Locked ? m.ThumbHold : m.ThumbFree;
            DrawThumb(g, m, fonts, thumb);
        }

        private static void DrawThumb(Graphics g, AluMetrics m, AluFonts fonts, RectangleF thumb)
        {
            float radius = m.ThumbRadiusPx;

            AluSurface.DrawDropShadow(g, thumb, radius, AluPalette.ThumbShadow, 3f * m.Scale, 6f * m.Scale);

            AluSurface.FillVerticalGradient(g, thumb, AluPalette.ThumbStops, AluPalette.ThumbPositions);

            AluSurface.DrawInnerTopLight(g, thumb, radius, AluPalette.ThumbTopLight, 1.5f * m.Scale);
            AluSurface.DrawInnerBottomEdge(g, thumb, radius, AluPalette.ThumbInset, 3f * m.Scale);

            using (Pen pen = new Pen(AluPalette.ThumbBorder, 1f))
                AluSurface.DrawRounded(g, thumb, radius, pen);

            // 左侧的 LOCK 铭牌
            RectangleF label = new RectangleF(
                thumb.Left + 10f * m.Scale,
                thumb.Top,
                thumb.Width * 0.6f,
                thumb.Height);
            AluText.Draw(g, "LOCK", fonts.ThumbLabel, AluPalette.ThumbText, label,
                AluTextAlign.Left, 0f, AluPalette.ThumbTextShadow, 1f);

            // 右侧的防滑棱
            float ridgeWidth = 2f * m.Scale;
            float ridgeGap = 3f * m.Scale;
            float ridgeHeight = 22f * m.Scale;
            float ridgeRight = thumb.Right - 10f * m.Scale;
            float startX = ridgeRight - (3f * ridgeWidth + 2f * ridgeGap);
            float y = thumb.Top + (thumb.Height - ridgeHeight) / 2f;

            using (SolidBrush ridgeBrush = new SolidBrush(AluPalette.GripRidge))
            using (SolidBrush highlightBrush = new SolidBrush(AluPalette.GripRidgeHighlight))
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = startX + i * (ridgeWidth + ridgeGap);
                    g.FillRectangle(ridgeBrush, x, y, ridgeWidth, ridgeHeight);
                    g.FillRectangle(highlightBrush, x + ridgeWidth, y, 1f * m.Scale, ridgeHeight);
                }
            }
        }
    }
}
