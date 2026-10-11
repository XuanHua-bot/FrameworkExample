namespace GameFramework
{
    /// <summary>
    /// UI打开策略， 声明重复打开规则，决定能否同时打开多个ui
    /// </summary>
    public enum UIOpenMode
    {
        Single,
        Multiple,
    }
    //ui所属layer
    public enum UILayer
    {
        /// <summary>
        /// 背景层
        /// </summary>
        Background,
        /// <summary>
        /// 普通页面层 HUD,设置, 背包等
        /// </summary>
        Normal,
        /// <summary>
        /// 弹窗层 确认框, 暂停窗口 等
        /// </summary>
        Popup,
        /// <summary>
        /// 系统层 Loading,短线提示 和系统错误提示等
        /// </summary>
        System,
        /// <summary>
        /// Toast层 保存成功, 获得奖励等 短生命周期ui
        /// </summary>
        Toast,
    }

    public enum UILifetime
    {
        /// <summary>
        /// 只属于当前场景， 随场景切换关闭/销毁
        /// </summary>
        Scene,
        /// <summary>
        /// 切换场景保留
        /// </summary>
        Persistent,
    }

    /// <summary>
    /// 页面被其他页面覆盖时的处理法方式
    /// </summary>
    public enum UICoverMode
    {
        KeepVisble,
        Hide,
        Close,
    }
}