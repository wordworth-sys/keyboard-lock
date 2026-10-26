namespace KeyboardLock.Alu
{
    /// <summary>面板的三种状态。</summary>
    public enum AluPanelState
    {
        /// <summary>未锁定。</summary>
        Unlocked,

        /// <summary>已锁定。</summary>
        Locked,

        /// <summary>键盘钩子安装失败，锁定功能不可用。</summary>
        Unavailable
    }

    /// <summary>面板上可交互区域的命中结果。</summary>
    public enum AluHitTarget
    {
        None,
        /// <summary>标题栏（拖动窗口）。</summary>
        TitleBar,
        /// <summary>最小化。</summary>
        Minimize,
        /// <summary>关闭到托盘。</summary>
        CloseToTray,
        /// <summary>凹槽模块（滑块槽 / 状态窗，点一下切换锁定）。</summary>
        ToggleModule,
        /// <summary>主操作按钮。</summary>
        MainButton
    }
}
