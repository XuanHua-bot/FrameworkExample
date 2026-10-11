using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 让字符按索引形成连续波浪，适合地图标记、任务目标和加载提示。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Wave")]
    public sealed class UITextWaveEffect : UITextAnimationEffect
    {
        [SerializeField] private float mAmplitude = 5f;
        [SerializeField, Min(0.01f)] private float mFrequency = 2f;
        [SerializeField, Min(0f)] private float mCharacterOffset = 0.35f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var phase = context.Elapsed / Mathf.Max(0.01f, context.Duration) *
                Mathf.PI * 2f * Mathf.Max(1f, mFrequency);
            state.Offset.y += Mathf.Sin(phase + context.CharacterIndex * mCharacterOffset) *
                mAmplitude;
        }
    }
}
