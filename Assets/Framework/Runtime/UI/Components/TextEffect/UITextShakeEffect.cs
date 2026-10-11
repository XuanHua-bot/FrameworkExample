using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 让字符产生轻微抖动。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Shake")]
    public sealed class UITextShakeEffect : UITextAnimationEffect
    {
        [SerializeField] private float mAmplitude = 1.5f;
        [SerializeField, Min(0.01f)] private float mFrequency = 18f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            // X/Y 使用同一个完整周期，避免循环边界出现跳变。
            var cycles = Mathf.Max(1, Mathf.RoundToInt(mFrequency));
            var phase = context.Elapsed / Mathf.Max(0.01f, context.Duration) * Mathf.PI * 2f * cycles;
            phase += context.CharacterIndex * 1.37f;
            // 循环效果不能乘入从 0 到 1 的入场曲线，否则每个周期的首尾振幅不同。
            state.Offset += new Vector3(Mathf.Sin(phase), Mathf.Cos(phase), 0f) * mAmplitude;
        }
    }
}
