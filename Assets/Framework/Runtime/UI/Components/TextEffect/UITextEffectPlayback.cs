using System;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 决定一组文字效果如何随时间播放。
    /// </summary>
    [Serializable]
    public sealed class UITextEffectPlayback
    {
        public enum Mode
        {
            Once,
            Loop
        }

        [Tooltip("Once 播放一次；Loop 到达持续时间后重新开始")]
        public Mode PlaybackMode = Mode.Once;

        [Tooltip("循环次数。-1 表示无限循环；仅对 Loop 生效")]
        public int LoopCount = -1;

        [Tooltip("开始播放前的延迟秒数")]
        public float Delay;

        public bool IsLooping => PlaybackMode == Mode.Loop && LoopCount != 0;

        public bool IsInfinite => IsLooping && LoopCount < 0;

        public float GetTotalDuration(float cycleDuration)
        {
            var safeCycleDuration = Mathf.Max(0.01f, cycleDuration);
            var cycles = PlaybackMode == Mode.Loop && LoopCount > 0 ? LoopCount : 1;
            return Mathf.Max(0f, Delay) + safeCycleDuration * cycles;
        }

        /// <summary>
        /// 将 Animator 的时间转换为当前范围使用的时间。
        /// 循环范围从零重新开始；一次性范围在结束后保持最终时间。
        /// </summary>
        public float GetElapsed(float elapsed, float duration)
        {
            var safeDuration = Mathf.Max(0.01f, duration);
            var activeElapsed = elapsed - Mathf.Max(0f, Delay);
            if (activeElapsed <= 0f)
            {
                return 0f;
            }

            if (!IsLooping)
            {
                return Mathf.Min(activeElapsed, safeDuration);
            }

            if (LoopCount > 0 && activeElapsed >= safeDuration * LoopCount)
            {
                return safeDuration;
            }

            // 在边界处返回周期终点，避免首尾两帧分别取不同状态。
            var cycleTime = Mathf.Repeat(activeElapsed, safeDuration);
            return cycleTime == 0f ? safeDuration : cycleTime;
        }
    }
}
