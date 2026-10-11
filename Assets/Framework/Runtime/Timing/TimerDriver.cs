using System;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 用于讲 每帧事件 转交给TimerService的类
    /// </summary>
    internal sealed class TimerDriver:MonoBehaviour
    {
        public event Action<float, float> Tick;

        private void Update()
        {
            Tick?.Invoke(Time.deltaTime, Time.unscaledDeltaTime);
        }
    }
}