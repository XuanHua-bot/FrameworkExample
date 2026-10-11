using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 在原始颜色和目标颜色之间平滑脉冲，不破坏 TMP 的透明度动画。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Color Pulse")]
    public sealed class UITextColorPulseEffect : UITextAnimationEffect
    {
        [SerializeField] private Color mTargetColor = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField, Min(0.01f)] private float mFrequency = 2f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var phase = context.Elapsed / Mathf.Max(0.01f, context.Duration) *
                Mathf.PI * 2f * Mathf.Max(1f, mFrequency);
            var amount = (Mathf.Sin(phase) + 1f) * 0.5f;
            var color = Color.Lerp(state.Color, mTargetColor, amount);
            color.a = state.Color.a;
            state.Color = color;
        }
    }
}
