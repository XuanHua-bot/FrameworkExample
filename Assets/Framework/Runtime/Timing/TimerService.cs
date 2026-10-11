using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    public class TimerService:ITimerService,IDeinit
    {
        private sealed class TimerTask
        {
            public int Id;//与TimerHandle.Id对应
            public float Remaining;//距离下次执行还剩多少秒
            public float Interval;//循环任务的  执行间隔
            public bool Repeat;//是否为重复执行
            public bool Cancelled;//遍历时 值标记取消 循环末尾再真正从list删除
            public TimerTimeMode TimeMode;//是否受时间缩放影响
            public Action Callback;//业务回调
        }
        /// <summary>
        /// 当前让在等待或循环中的所有任务
        /// </summary>
        private readonly List<TimerTask> mTasks = new List<TimerTask>();

        private TimerDriver mDriver;//update的桥接组件
        private int mNextId = 1;//任务的递增编号，留给无效的timerhandle


        public TimerService()
        {
            var driverObject = new GameObject("[GameFramework] TimerDriver");
            //切换场景是不让框架即使服务失效， 会话结束再由 deinit销毁
            UnityEngine.Object.DontDestroyOnLoad(driverObject);
            mDriver = driverObject.AddComponent<TimerDriver>();
            //每帧由driver把两种deltatime 交给类
            mDriver.Tick += OnTick;
        }

       

        public TimerHandle Delay(float seconds, Action callBack, TimerTimeMode timeMode = TimerTimeMode.Scaled)
        {
            return Schedule(seconds, callBack, repeat: false, timeMode);//repeat为命名参数 ， 把bool值传给方法的repeat形参 
        }

        public TimerHandle Every(float interval, Action callBack, TimerTimeMode timeMode = TimerTimeMode.Scaled)
        {
            //0秒循环判断
            if (interval<=0f)throw new ArgumentOutOfRangeException(nameof(interval));
            return Schedule(interval, callBack, repeat: true, timeMode);
           
        }

        

        public bool Cancel(TimerHandle handle)
        {
            if (!handle.IsValid) return false;//默认句柄或者已被清理的句柄不能取消任何任务
            foreach (var task in mTasks)
            {
                //如果任务编号不同 或者已经取消责继续找下一个
                if (task.Id!=handle.Id ||task.Cancelled) continue;
                //不立刻remove 回调可能正处于mtask的遍历中  此处进行标记
                task.Cancelled = true;
                return true;
            }

            return false;

        }

        public void CancelAll()
        {
            foreach (var task in mTasks)
            {
                task.Cancelled = true;
            }
        }

        public void Deinit()
        {
            //Architecture 结束是， 先解除C#的委托关系
            if (mDriver!=null)
            {
                mDriver.Tick -= OnTick;
                UnityEngine.Object.Destroy(mDriver.gameObject);
                mDriver = null;
            }

            mTasks.Clear();
            mNextId = 1;//下次创建新的 timerService 编号从1开始
        }
        
        private TimerHandle Schedule(float seconds, Action callBack, bool repeat, TimerTimeMode timeMode)
        {
            if (seconds<0f)throw new ArgumentOutOfRangeException(nameof(seconds));//延迟不能为负数，下次tick时执行
            if (callBack==null)throw new ArgumentNullException(nameof(callBack));//没有回调 没有意义

            var task = new TimerTask
            {
                Id = mNextId++,
                //首次等待seconds,循环时也用interval重置
                Remaining = seconds,
                Repeat = repeat,
                Interval = seconds,
                TimeMode = timeMode,
                Callback = callBack,

            };
            mTasks.Add(task);
            
            return new TimerHandle(task.Id);//返回编号 用于注销 计时器
        }
        

        
        /*
         * 遍历所有任务
    →       Remaining -= deltaTime

            Remaining > 0
            → 还没到时间，处理下一个

            Remaining <= 0
→           循环任务：Remaining 重置为 Interval
→           单次任务：标记 Cancelled
→           执行 Callback
         */
        private void OnTick(float deltaTime, float unscaledDeltaTime)
        {
            var taskCountAtStart = mTasks.Count;
            for (int i = 0; i < taskCountAtStart; i++)
            {
                var task = mTasks[i];
                
                //已被cancel的任务不再减少时间 和 出发回调
                if (task.Cancelled)continue;
                //每个任务独立选择要使用的时间尺度
                var delta = task.TimeMode==TimerTimeMode.Scaled? deltaTime:unscaledDeltaTime;
                task.Remaining -= delta;
                //未到时间，本帧任务处理完毕
                if (task.Remaining>0f)continue;

                if (task.Repeat) task.Remaining = task.Interval;//如果是 循环， 那么重置倒计时
                else
                {
                    task.Cancelled = true;
                }

                try
                {
                    task.Callback.Invoke();//回调可能报错，记录异常
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            // 全部处理完后：再真正删除被标记取消的任务
            for (int i = mTasks.Count-1; i >=0 ; i--)
            {
                if (mTasks[i].Cancelled)
                {
                    mTasks.RemoveAt(i);
                }
            }
        }
    }
}
