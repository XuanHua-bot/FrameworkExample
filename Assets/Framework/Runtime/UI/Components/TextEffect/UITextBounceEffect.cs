using UnityEngine;

namespace GameFramework
{
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Bounce")]
    public sealed class UITextBounceEffect : UITextAnimationEffect
    {
        [SerializeField] private float mHeight = 7f;
        [SerializeField, Min(0f)] private float mCharacterOffset = 0.25f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var phase = context.Elapsed / Mathf.Max(0.01f, context.Duration) * Mathf.PI * 2f;
            state.Offset.y += Mathf.Abs(Mathf.Sin(phase + context.CharacterIndex * mCharacterOffset)) * mHeight;
        }
    }
}
