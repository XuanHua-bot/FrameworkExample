using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 字符逐渐从透明变为不透明。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Fade In")]
    public sealed class UITextFadeInEffect : UITextAnimationEffect
    {
        [SerializeField, Min(0f)] private float mCharacterDelay = 0.02f;

        public override float GetDuration(int characterCount)
        {
            return GetDuration() + Mathf.Max(0, characterCount - 1) * mCharacterDelay;
        }

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            state.Alpha *= Evaluate(context.Elapsed - (context.CharacterIndex - context.RangeStart) * mCharacterDelay);
        }
    }
}
