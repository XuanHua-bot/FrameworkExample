using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 让字符从较小尺寸放大到正常尺寸。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Scale In")]
    public sealed class UITextScaleInEffect : UITextAnimationEffect
    {
        [SerializeField, Range(0f, 1f)] private float mStartScale = 0.7f;
        [SerializeField, Min(0f)] private float mCharacterDelay = 0.02f;

        public override float GetDuration(int characterCount)
        {
            return GetDuration() + Mathf.Max(0, characterCount - 1) * mCharacterDelay;
        }

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var progress = Evaluate(context.Elapsed - (context.CharacterIndex - context.RangeStart) * mCharacterDelay);
            state.Scale *= Mathf.Lerp(mStartScale, 1f, progress);
        }
    }
}
