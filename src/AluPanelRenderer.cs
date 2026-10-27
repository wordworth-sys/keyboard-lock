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
    ///   · 布局矩形一律来自 <see cref="AluMetrics"/>；只有按字形动态排布的局部（页脚键帽、刻度标签）例外。
    ///
    /// 局部重绘：<see cref="Draw"/> 接收失效矩形，与之不相交的区块整段跳过。
    /// 悬停窗口控制键 / 按下主键这类高频小变化，只重画对应区块（见 <see cref="WindowControlsBounds"/>）。
    /// </summary>
    internal sealed class AluPanelRenderer : IDisposable
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

        /// <summary>主操作键处于按下状态。</summary>
        public bool PressMainButton { get; set; }

        /// <summary>当前尺寸下的布局度量（未绘制过则按默认尺寸生成）。</summary>
        public AluMetrics Metrics
        {
            get
            {
                if (metrics == null)
                    metrics = AluMetrics.FromSize(cachedSize == Size.Empty ? new Size((int)AluMetrics.DesignWidth, (int)AluMetrics.DesignHeight) : cachedSize);
                return metrics;
            }
        }

        /// <summary>窗口控制键区块（含投影外扩）的失效矩形。</summary>
        public Rectangle WindowControlsBounds
        {
            get { return Bounds(Metrics.WinSlot, 3f * Metrics.Scale); }
        }

        /// <summary>主操作键区块（含投影外扩）的失效矩形。</summary>
        public Rectangle MainButtonBounds
        {
            get { return Bounds(Metrics.MainSlot, 6f * Metrics.Scale); }
        }

        /// <summary>绘制面板中与 clip 相交的部分。clip 取 PaintEventArgs.ClipRectangle。</summary>
        public void Draw(Graphics g, Size size, Rectangle clip)
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

            AluMetrics m = metrics;
            RectangleF c = clip;

            GraphicsState saved = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                // 必须是 Default：HighQuality 会启用伽马校正混合，半透明高光会明显发白，
                // 与浏览器（sRGB 直接混合）的观感对不上。
                g.CompositingQuality = CompositingQuality.Default;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                // 机身底色永远要画（它是其它区块的背景）；GDI+ 会按 clip 裁掉看不见的部分。
                AluChassisPainter.Draw(g, m);

                if (c.IntersectsWith(m.Module))
                {
                    AluChassisPainter.DrawRecessedModule(g, m);
                    AluSliderBayPainter.Draw(g, m, fonts, State);
                    AluStatusPainter.Draw(g, m, fonts, State);
                }

                if (c.IntersectsWith(Inflate(m.MainSlot, 6f * m.Scale)))
                    AluMainButtonPainter.Draw(g, m, fonts, State, PressMainButton);

                if (c.IntersectsWith(m.Footer))
                    AluFooterPainter.Draw(g, m, fonts);

                if (c.IntersectsWith(m.TitleBar))
                    AluTitleBarPainter.Draw(g, m, fonts, State, HoverMinimize, HoverClose);
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

        private static RectangleF Inflate(RectangleF r, float by)
        {
            r.Inflate(by, by);
            return r;
        }

        private static Rectangle Bounds(RectangleF r, float inflate)
        {
            return Rectangle.Ceiling(Inflate(r, inflate));
        }
    }
}
