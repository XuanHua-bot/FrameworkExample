using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 翻转入场：字符一开始几乎侧对着镜头（横向压到很窄），
    /// 随后像翻牌一样横向展开并冲过宽度，同时纵向从拉伸状态回到正常比例，
    /// 制造"立起来正对镜头"的透视错觉（依赖非等比缩放）。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Flip In")]
    public sealed class UITextFlipInEffect : UITextAnimationEffect
    {
        [SerializeField, Min(0f)] private float mCharacterDelay = 0.05f;
        [SerializeField, Range(0.02f, 0.4f)] private float mStartFlatten = 0.08f;
        [SerializeField, Range(1f, 1.6f)] private float mOvershoot = 1.15f;
        [SerializeField, Range(0.4f, 0.9f)] private float mSettleRatio = 0.65f;
        [SerializeField, Range(0f, 0.5f)] private float mStretch = 0.18f;

        public override float GetDuration(int characterCount)
        {
            return GetDuration() + Mathf.Max(0, characterCount - 1) * mCharacterDelay;
        }

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var progress = Evaluate(context.Elapsed - (context.CharacterIndex - context.RangeStart) * mCharacterDelay);
            var settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / mSettleRatio));
            var width = progress < mSettleRatio
                ? Mathf.Lerp(mStartFlatten, mOvershoot, settle)
                : Mathf.Lerp(mOvershoot, 1f,
                    Mathf.SmoothStep(0f, 1f, (progress - mSettleRatio) / Mathf.Max(0.01f, 1f - mSettleRatio)));

            // 展开过程中的纵向拉伸，配合横向宽度变化形成翻转错觉。
            var height = 1f + mStretch * (1f - settle);
            state.ScaleXY *= new Vector2(width, height);
            state.Alpha *= Mathf.Clamp01(progress * 5f);
        }
    }
}
