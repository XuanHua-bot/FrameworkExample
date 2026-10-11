using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 让字符按字符索引产生轻微旋转，适合标题、提示和强调文本。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Rotate")]
    public sealed class UITextRotateEffect : UITextAnimationEffect
    {
        [SerializeField] private float mAngle = 8f;
        [SerializeField, Min(0f)] private float mCharacterOffset = 0.15f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var phase = context.Elapsed / Mathf.Max(0.01f, context.Duration) * Mathf.PI * 2f;
            state.Rotation += Mathf.Sin(phase + context.CharacterIndex * mCharacterOffset) *
                mAngle;
        }
    }
}
