using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 自定义平滑渐变流光：在文字上铺开配置色带并丝滑流动。
    /// 解决了多颜色段拼接时的跳变与硬转折，色环两端无缝闭合，
    /// 每帧采样完全连续，保证毫无顿挫与卡顿感。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Gradient Flow")]
    public sealed class UITextGradientFlowEffect : UITextAnimationEffect
    {
        [SerializeField] private List<Color> mColors = new List<Color>
        {
            new Color(1f, 0.78f, 0.28f, 1f),  // 亮金
            new Color(1f, 0.42f, 0.32f, 1f),  // 珊瑚橘
            new Color(0.92f, 0.35f, 0.85f, 1f),// 霓虹品红
            new Color(0.38f, 0.55f, 1f, 1f),  // 科技蓝
            new Color(0.28f, 0.92f, 0.85f, 1f) // 青绿
        };

        [Tooltip("色带在整段文字上的展开跨度，越大过渡越细腻丰富")]
        [SerializeField, Range(0.1f, 3f)] private float mSpread = 0.85f;

        [Tooltip("流动速度（周/秒）。直接按真实时间推进，不依赖动画周期，确保绝不顿挫卡顿")]
        [SerializeField, Range(0f, 3f)] private float mSpeed = 0.65f;

        [Tooltip("上下半部采样的错开量。让每个字本身呈现自然的自上而下渐层")]
        [SerializeField, Range(-0.5f, 0.5f)] private float mVerticalShift = 0.12f;

        [SerializeField] private bool mReverse;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            if (mColors == null || mColors.Count == 0)
            {
                return;
            }

            // 直接按 Elapsed * mSpeed 推进相位，纯平滑连续时间，避免周期取模导致的硬转折
            var timeOffset = context.Elapsed * mSpeed;
            var pos = context.RangeProgress * mSpread;
            if (mReverse)
            {
                pos = -pos;
            }

            var t = pos - timeOffset;
            var alpha = state.Color.a / 255f;

            var topColor = EvaluateGradient(t);
            topColor.a = alpha;
            state.Color = topColor;

            if (Mathf.Abs(mVerticalShift) > 0.001f)
            {
                var botColor = EvaluateGradient(t + mVerticalShift);
                botColor.a = alpha;
                state.ColorBottom = botColor;
                state.Gradient = true;
            }
            else
            {
                state.Gradient = false;
            }
        }

        /// <summary>
        /// 环形闭合平滑采样：在任意点 t 处插值，把首尾两端平滑衔接为一个无缝色相环。
        /// </summary>
        private Color EvaluateGradient(float t)
        {
            var count = mColors.Count;
            if (count == 1)
            {
                return mColors[0];
            }

            // 归一化到 [0, 1) 的无缝色环
            var normalized = t - Mathf.Floor(t);
            var scaled = normalized * count;
            var index = Mathf.FloorToInt(scaled);
            var frac = scaled - index;

            // 平滑 S 型曲线插值，彻底消除两色交界处的折角感
            var smoothFrac = frac * frac * (3f - 2f * frac);

            var c0 = mColors[index % count];
            var c1 = mColors[(index + 1) % count];

            return Color.Lerp(c0, c1, smoothFrac);
        }
    }
}
