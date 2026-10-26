using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 铝合金面板渲染器：把「机身 + 标题栏 + 凹槽模块 + 主键 + 页脚」组装成一幅画面。
    ///
    /// 分层原则：
    ///   · 本类只负责状态、命中测试与绘制编排；
    ///   · 每个视觉区块由 src/alu/ 下的独立 Painter 负责，互不直接依赖；
    ///   · 布局坐标一律来自 <see cref="AluMetrics"/>，绘制代码不自己算位置。
    /// </summary>
    public sealed class AluPanelRenderer : IDisposable
    {
        private AluMetrics metrics;
        private AluFonts fonts;
        private Size cachedSize = Size.Empty;

        private bool disposed;

        /// <summary>面板当前状态（未锁定 / 已锁定 / 钩子不可用）。</summary>
        public AluPanelState State { get; set; }

        /// <summary>指针悬停：最小化键。</summary>
        public bool HoverMinimize { get; set; }

        /// <summary>指针悬停：关闭键。</summary>
        public bool HoverClose { get; set; }

        /// <summary>指针悬停：主操作键。</summary>
        public bool HoverMainButton { get; set; }

        /// <summary>主操作键处于按下状态。</summary>
        public bool PressMainButton { get; set; }

        /// <summary>当前尺寸下的布局度量（未绘制过则按默认尺寸生成）。</summary>
        public AluMetrics Metrics
        {
            get
            {
                if (metrics == null)
                    metrics = AluMetrics.FromSize(cachedSize == Size.Empty ? new Size(400, 264) : cachedSize);
                return metrics;
            }
        }

        /// <summary>绘制整块面板。</summary>
        public void Draw(Graphics g, Size size)
        {
            if (g == null || disposed)
                return;

            if (cachedSize != size)
            {
                cachedSize = size;
                metrics = AluMetrics.FromSize(size);

                // 字号跟着缩放走，尺寸变了必须重建字体集。
                if (fonts != null)
                {
                    fonts.Dispose();
                    fonts = null;
                }
            }

            if (fonts == null)
                fonts = new AluFonts(metrics.Scale);

            GraphicsState saved = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                // 必须是 Default：HighQuality 会启用伽马校正混合，半透明高光会明显发白，
                // 与浏览器（sRGB 直接混合）的观感对不上。
                g.CompositingQuality = CompositingQuality.Default;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                AluChassisPainter.Draw(g, metrics);
                AluChassisPainter.DrawRecessedModule(g, metrics);
                AluSliderBayPainter.Draw(g, metrics, fonts, State);
                AluStatusPainter.Draw(g, metrics, fonts, State);
                AluMainButtonPainter.Draw(g, metrics, fonts, State, PressMainButton);
                AluFooterPainter.Draw(g, metrics, fonts);
                AluTitleBarPainter.Draw(g, metrics, fonts, State, HoverMinimize, HoverClose);
            }
            finally
            {
                g.Restore(saved);
            }
        }

        /// <summary>命中测试：返回指针落在哪个可交互区域上。</summary>
        public AluHitTarget HitTest(Point point)
        {
            AluMetrics m = Metrics;

            if (m.MinimizeButton.Contains(point))
                return AluHitTarget.Minimize;
            if (m.CloseButton.Contains(point))
                return AluHitTarget.CloseToTray;
            if (m.MainButton.Contains(point))
                return AluHitTarget.MainButton;
            if (m.Module.Contains(point))
                return AluHitTarget.ToggleModule;
            if (m.TitleBar.Contains(point))
                return AluHitTarget.TitleBar;

            return AluHitTarget.None;
        }

        /// <summary>某个区域当前是否可交互（钩子不可用时锁定操作失效）。</summary>
        public bool IsInteractive(AluHitTarget target)
        {
            if (target == AluHitTarget.ToggleModule || target == AluHitTarget.MainButton)
                return State != AluPanelState.Unavailable;
            return true;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            if (fonts != null)
            {
                fonts.Dispose();
                fonts = null;
            }
        }
    }
}
