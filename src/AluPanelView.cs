using System;
using System.Drawing;
using System.Windows.Forms;

using KeyboardLock.Alu;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 承载 <see cref="AluPanelRenderer"/> 的控件：把鼠标事件翻译成面板动作，
    /// 并维护悬停 / 按下的视觉状态。窗体只关心「用户想做什么」。
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

        /// <summary>面板状态；变更后立即重绘。</summary>
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
            renderer.Draw(e.Graphics, ClientSize);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            AluHitTarget target = renderer.HitTest(e.Location);
            if (target == hoverTarget)
                return;

            hoverTarget = target;
            renderer.HoverMinimize = target == AluHitTarget.Minimize;
            renderer.HoverClose = target == AluHitTarget.CloseToTray;
            renderer.HoverMainButton = target == AluHitTarget.MainButton;
            UpdateCursor(target);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            hoverTarget = AluHitTarget.None;
            renderer.HoverMinimize = false;
            renderer.HoverClose = false;
            renderer.HoverMainButton = false;
            UpdateCursor(AluHitTarget.None);
            Invalidate();
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
                OnDragRequested();
                return;
            }

            if (!renderer.IsInteractive(target))
                return;

            pressedTarget = target;
            renderer.PressMainButton = target == AluHitTarget.MainButton;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button != MouseButtons.Left)
                return;

            AluHitTarget target = renderer.HitTest(e.Location);
            AluHitTarget released = pressedTarget;
            pressedTarget = AluHitTarget.None;
            renderer.PressMainButton = false;
            Capture = false;
            Invalidate();

            if (released == AluHitTarget.None || released != target)
                return;

            switch (released)
            {
                case AluHitTarget.Minimize:
                    OnMinimizeRequested();
                    break;
                case AluHitTarget.CloseToTray:
                    OnCloseToTrayRequested();
                    break;
                case AluHitTarget.ToggleModule:
                case AluHitTarget.MainButton:
                    OnToggleRequested();
                    break;
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

        private void OnToggleRequested()
        {
            EventHandler handler = ToggleRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void OnMinimizeRequested()
        {
            EventHandler handler = MinimizeRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void OnCloseToTrayRequested()
        {
            EventHandler handler = CloseToTrayRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void OnDragRequested()
        {
            EventHandler handler = DragRequested;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
