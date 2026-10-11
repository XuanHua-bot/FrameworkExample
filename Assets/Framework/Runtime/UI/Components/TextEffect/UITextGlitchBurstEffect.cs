using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 专业 AE 信号故障重组 (Cinematic Glitch Burst):
    /// 模拟黑客入侵/系统异常时的多波段电磁冲击。
    /// 具备清晰的三段式故障波：急速扫描线横滚、水平失真错动与通道电磁染色。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Glitch Burst")]
    public sealed class UITextGlitchBurstEffect : UITextAnimationEffect
    {
        [Tooltip("故障冲击强度")]
        [SerializeField, Range(3f, 25f)] private float mBurstShift = 12f;

        [Tooltip("电磁脉冲高光色（冷电青）")]
        [SerializeField] private Color mPulseColor = new Color(0.2f, 0.95f, 1f, 1f);

        [Tooltip("反向极化色（电子紫红）")]
        [SerializeField] private Color mInvertColor = new Color(1f, 0.2f, 0.75f, 1f);

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var duration = Mathf.Max(0.01f, context.Duration);
            var phase = Mathf.Repeat(context.Elapsed / duration, 1f);

            // 构造 3 个极具穿透力且不均匀分布的爆裂时间窗口
            var b1 = Pulse(phase, 0.12f, 0.08f);
            var b2 = Pulse(phase, 0.38f, 0.14f);
            var b3 = Pulse(phase, 0.72f, 0.09f);

            var power = Mathf.Clamp01(b1 * 1.2f + b2 * 1.5f + b3 * 1.1f);
            if (power <= 0.001f)
            {
                return; // 常态下 100% 干净
            }

            var charIdx = context.CharacterIndex - context.RangeStart;

            // 仿显像管水平撕裂与高频颤抖
            var jitter = Mathf.Sin(charIdx * 19.3f + phase * 180f);
            var scanline = Mathf.Cos(phase * 90f + charIdx * 3.7f);

            state.Offset.x += (jitter * 0.7f + scanline * 0.3f) * mBurstShift * power;
            state.Offset.y += Mathf.Sin(charIdx * 11.2f + phase * 60f) * (mBurstShift * 0.35f) * power;
            state.Skew += jitter * 0.28f * power;

            // 电磁染色
            var alpha = state.Color.a / 255f;
            var top = Color.Lerp((Color)state.Color, mPulseColor, power * 0.8f);
            var bot = Color.Lerp((Color)state.Color, mInvertColor, power * 0.8f);
            top.a = alpha;
            bot.a = alpha;

            state.Color = top;
            state.ColorBottom = bot;
            state.Gradient = true;

            // 剧烈故障瞬时强闪
            if (power > 0.65f)
            {
                state.Scale *= 1f + 0.12f * power;
            }
        }

        private static float Pulse(float phase, float start, float width)
        {
            if (phase < start || phase > start + width) return 0f;
            var p = (phase - start) / width;
            return Mathf.Sin(p * Mathf.PI);
        }
    }
}
