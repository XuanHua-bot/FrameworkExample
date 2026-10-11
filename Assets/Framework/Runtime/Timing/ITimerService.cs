using System;

namespace GameFramework
{
    public interface ITimerService:IUtility
    {
        TimerHandle Delay(float seconds,Action callBack,TimerTimeMode timeMode = TimerTimeMode.Scaled);
        TimerHandle Every(float interval,Action callBack,TimerTimeMode timeMode = TimerTimeMode.Scaled);
        bool Cancel(TimerHandle handle);
        void CancelAll();
    }
}