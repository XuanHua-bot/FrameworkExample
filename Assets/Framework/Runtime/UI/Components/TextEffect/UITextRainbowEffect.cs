using UnityEngine;

namespace GameFramework
{
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Rainbow")]
    public sealed class UITextRainbowEffect : UITextAnimationEffect
    {
        [SerializeField, Min(1)] private int mCycles = 2;
        [SerializeField, Range(0.01f, 1f)] private float mCharacterOffset = 0.08f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            // 使用整数周期，范围循环结束时回到同一色相，避免首尾跳变。
            var phase = context.Elapsed / Mathf.Max(0.01f, context.Duration) * Mathf.Max(1, mCycles);
            var hue = Mathf.Repeat(phase + context.CharacterIndex * mCharacterOffset, 1f);
            var color = Color.HSVToRGB(hue, 0.65f, 1f);
            color.a = state.Color.a / 255f;
            state.Color = color;
        }
    }
}
