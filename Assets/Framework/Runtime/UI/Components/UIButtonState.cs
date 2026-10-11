namespace GameFramework
{
    public enum UIButtonState
    {
        /// <summary>
        /// 按钮可以接受用户点击
        /// </summary>
        Normal,
        /// <summary>
        /// 按钮等待业务操作完成
        /// busy状态会阻止重复点击
        /// </summary>
        Busy,
        /// <summary>
        /// 按钮因业务条件不满足而不可用
        /// </summary>
        Disabled,
        /// <summary>
        /// 按钮处于规定的冷却时间内
        /// </summary>
        Cooldown,
    }
}