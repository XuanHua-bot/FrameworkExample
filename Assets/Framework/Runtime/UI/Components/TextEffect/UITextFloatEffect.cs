using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 让字符持续上下浮动。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Float")]
    public sealed class UITextFloatEffect : UITextAnimationEffect
    {
        [SerializeField] private float mAmplitude = 2f;
        [SerializeField, Min(0.01f)] private float mFrequency = 4f;
        [SerializeField, Min(0f)] private float mCharacterOffset = 0.08f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            // 循环效果使用归一化循环进度计算相位，保证每个循环结束时回到同一状态。
            var cycles = Mathf.Max(1, Mathf.RoundToInt(mFrequency));
            var phase = context.Elapsed / Mathf.Max(0.01f, context.Duration) * Mathf.PI * 2f * cycles;
            phase += context.CharacterIndex * mCharacterOffset;
            state.Offset.y += Mathf.Sin(phase) * mAmplitude;
        }
    }
}
