using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 逐字过冲弹入：字符从很小的尺寸、略微上方落下并放大，
    /// 先冲过目标尺寸再回弹落位，同时淡入。常见的 AE 式文字入场。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Pop In")]
    public sealed class UITextPopInEffect : UITextAnimationEffect
    {
        [SerializeField, Range(0f, 1f)] private float mStartScale = 0.2f;
        [SerializeField, Range(1f, 2f)] private float mOvershoot = 1.18f;
        [SerializeField, Range(0.3f, 0.9f)] private float mSettleRatio = 0.62f;
        [SerializeField, Min(0f)] private float mCharacterDelay = 0.05f;
        [SerializeField, Min(0f)] private float mDropOffset = 16f;

        public override float GetDuration(int characterCount)
        {
            return GetDuration() + Mathf.Max(0, characterCount - 1) * mCharacterDelay;
        }

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var progress = Evaluate(context.Elapsed - (context.CharacterIndex - context.RangeStart) * mCharacterDelay);
            var settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / mSettleRatio));
            var scale = progress < mSettleRatio
                ? Mathf.Lerp(mStartScale, mOvershoot, settle)
                : Mathf.Lerp(mOvershoot, 1f,
                    Mathf.SmoothStep(0f, 1f, (progress - mSettleRatio) / Mathf.Max(0.01f, 1f - mSettleRatio)));

            state.Scale *= scale;
            // 从上方落下，落位后位移归零。
            state.Offset.y += mDropOffset * (1f - settle);
            state.Alpha *= Mathf.Clamp01(progress * 4f);
        }
    }
}
