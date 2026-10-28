using System;
using System.Drawing;
using System.Windows.Forms;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 承载 <see cref="AluPanelRenderer"/> 的控件：把鼠标事件翻译成面板动作，
    /// 并维护悬停 / 按下的视觉状态。窗体只关心「用户想做什么」。
    ///
    /// 重绘策略：锁定状态变化才整板失效；悬停窗口控制键只失效右上角那一小块，
    /// 按下 / 松开主键只失效主键槽。悬停主键和凹槽模块没有视觉反馈，不触发重绘。
    /// </summary>
    internal sealed class AluPanelView : Control
    {
        private readonly AluPanelRenderer renderer = new AluPanelRenderer();
        private AluHitTarget pressedTarget = AluHitTarget.None;
        private AluHitTarget hoverTarget = AluHitTarget.None;

        /// <summary>点一下凹槽模块或主操作键（切换锁定）。</summary>
        public event EventHandler ToggleRequested;

        /// <summary>点最小化。</summary>
        public event EventHandler MinimizeRequested;

        /// <summary>点关闭（收进托盘）。</summary>
        public event EventHandler CloseToTrayRequested;

        /// <summary>在标题栏上按下鼠标（拖动窗口）。</summary>
        public event EventHandler DragRequested;

        public AluPanelView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            DoubleBuffered = true;
            TabStop = false;
            BackColor = AluPalette.ChassisMid;
        }

        /// <summary>面板状态；变更后整板重绘（标题、滑块、状态窗、主键都会变）。</summary>
        public AluPanelState State
        {
            get { return renderer.State; }
            set
            {
                if (renderer.State == value)
                    return;
                renderer.State = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            renderer.Draw(e.Graphics, ClientSize, e.ClipRectangle);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SetHover(renderer.HitTest(e.Location));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetHover(AluHitTarget.None);
        }

        private void SetHover(AluHitTarget target)
        {
            if (target == hoverTarget)
                return;

            bool wasOnWinControl = IsWindowControl(hoverTarget);
            hoverTarget = target;
            renderer.HoverMinimize = target == AluHitTarget.Minimize;
            renderer.HoverClose = target == AluHitTarget.CloseToTray;
            UpdateCursor(target);

            if (wasOnWinControl || IsWindowControl(target))
                Invalidate(renderer.WindowControlsBounds);
        }

        private static bool IsWindowControl(AluHitTarget target)
        {
            return target == AluHitTarget.Minimize || target == AluHitTarget.CloseToTray;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
                return;

            AluHitTarget target = renderer.HitTest(e.Location);

            if (target == AluHitTarget.TitleBar)
            {
                // 交给系统原生移动循环，行为与拖动标题栏一致。
                Raise(DragRequested);
                return;
            }

            if (!renderer.IsInteractive(target))
                return;

            pressedTarget = target;
            Capture = true;

            if (target == AluHitTarget.MainButton)
            {
                renderer.PressMainButton = true;
                Invalidate(renderer.MainButtonBounds);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button != MouseButtons.Left)
                return;

            AluHitTarget target = renderer.HitTest(e.Location);
            AluHitTarget released = pressedTarget;
            ClearPressedState();
            Capture = false;

            if (released == AluHitTarget.None || released != target)
                return;

            switch (released)
            {
                case AluHitTarget.Minimize:
                    Raise(MinimizeRequested);
                    break;
                case AluHitTarget.CloseToTray:
                    Raise(CloseToTrayRequested);
                    break;
                case AluHitTarget.ToggleModule:
                case AluHitTarget.MainButton:
                    Raise(ToggleRequested);
                    break;
            }
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);

            // 按住鼠标期间被切走焦点（如快捷键切换置顶、Alt+Tab）导致失去捕获时，及时复位按下视觉态。
            if (!Capture)
                ClearPressedState();
        }

        private void ClearPressedState()
        {
            pressedTarget = AluHitTarget.None;
            if (renderer.PressMainButton)
            {
                renderer.PressMainButton = false;
                Invalidate(renderer.MainButtonBounds);
            }
        }

        private void UpdateCursor(AluHitTarget target)
        {
            switch (target)
            {
                case AluHitTarget.TitleBar:
                    Cursor = Cursors.SizeAll;
                    break;
                case AluHitTarget.Minimize:
                case AluHitTarget.CloseToTray:
                case AluHitTarget.ToggleModule:
                case AluHitTarget.MainButton:
                    Cursor = Cursors.Hand;
                    break;
                default:
                    Cursor = Cursors.Default;
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                renderer.Dispose();
            base.Dispose(disposing);
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
