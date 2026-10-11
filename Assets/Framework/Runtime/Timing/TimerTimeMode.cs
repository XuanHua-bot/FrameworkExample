namespace GameFramework
{
    public enum TimerTimeMode
    {
        /// <summary>
        /// 使用time.deltaTime;暂停游戏时，计时也暂停
        /// </summary>
        Scaled,
        /// <summary>
        /// 使用Time.unscaleDeltaTime;暂停菜单 ，倒计时ui仍可继续
        /// </summary>
        Unscaled,
    }
}
