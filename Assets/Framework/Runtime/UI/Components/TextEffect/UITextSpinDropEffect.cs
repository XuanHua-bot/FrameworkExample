using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 旋转坠落入场：字符从上方带着倾斜旋转落下，接近落点时加速，
    /// 落地瞬间横向压扁（挤压回弹，需要非等比缩放），再弹回正常比例。
    /// 相邻字符的旋转方向相反，整体呈现左右交替的洒落感。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Spin Drop")]
    public sealed class UITextSpinDropEffect : UITextAnimationEffect
    {
        [SerializeField, Min(0f)] private float mCharacterDelay = 0.06f;
        [SerializeField, Range(30f, 360f)] private float mStartRotation = 140f;
        [SerializeField, Min(0f)] private float mFallHeight = 70f;
        [SerializeField, Range(0.4f, 0.9f)] private float mLandRatio = 0.7f;
        [SerializeField, Range(0f, 0.4f)] private float mSquash = 0.22f;
        [SerializeField, Min(0.01f)] private float mSquashWindow = 0.18f;

        public override float GetDuration(int characterCount)
        {
            return GetDuration() + Mathf.Max(0, characterCount - 1) * mCharacterDelay;
        }

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var progress = Evaluate(context.Elapsed - (context.CharacterIndex - context.RangeStart) * mCharacterDelay);
            var land = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / mLandRatio));
            var direction = (context.CharacterIndex & 1) == 0 ? 1f : -1f;

            // 位移用二次曲线，做出加速下落的重量感。
            var fall = (1f - land) * (1f - land);
            state.Offset.y += mFallHeight * fall;
            state.Rotation += direction * mStartRotation * fall;

            // 落地瞬间的挤压：横向拉宽、纵向压扁，然后回弹到正常比例。
            var squash = Mathf.Clamp01(1f - Mathf.Abs(progress - mLandRatio) / Mathf.Max(0.01f, mSquashWindow)) * mSquash;
            state.ScaleXY *= new Vector2(1f + squash, 1f - squash);

            state.Alpha *= Mathf.Clamp01(progress * 4f);
        }
    }
}
