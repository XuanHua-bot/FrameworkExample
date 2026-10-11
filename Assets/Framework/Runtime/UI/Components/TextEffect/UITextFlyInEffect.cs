using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 飞入汇聚：每个字符从各自随机方向的远处带着旋转飞回来，
    /// 到达时轻微过冲再落位，整体像碎片被吸回成一句话。
    /// 随机方向由字符索引决定，因此每次播放结果稳定、不会抖动。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Fly In")]
    public sealed class UITextFlyInEffect : UITextAnimationEffect
    {
        [SerializeField, Min(0f)] private float mCharacterDelay = 0.04f;
        [SerializeField, Min(0f)] private float mScatterRadius = 90f;
        [SerializeField, Range(0f, 360f)] private float mStartRotation = 200f;
        [SerializeField, Range(0f, 1f)] private float mRadiusJitter = 0.5f;
        [SerializeField, Range(0f, 1f)] private float mArriveRatio = 0.75f;
        [SerializeField, Range(0f, 1f)] private float mOvershoot = 0.14f;

        public override float GetDuration(int characterCount)
        {
            return GetDuration() + Mathf.Max(0, characterCount - 1) * mCharacterDelay;
        }

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var index = context.CharacterIndex;
            var progress = Evaluate(context.Elapsed - (index - context.RangeStart) * mCharacterDelay);
            var arrive = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / mArriveRatio));
            var remaining = 1f - arrive;

            // 随机方向 + 随机距离：让汇聚看起来是"碎片"而不是整齐的直线飞入。
            var angle = Hash01(index, 11) * Mathf.PI * 2f;
            var radius = mScatterRadius * Mathf.Lerp(1f - mRadiusJitter, 1f, Hash01(index, 12));
            state.Offset += new Vector3(Mathf.Cos(angle) * radius * remaining, Mathf.Sin(angle) * radius * remaining, 0f);

            var spin = Hash01(index, 13) < 0.5f ? -1f : 1f;
            state.Rotation += spin * mStartRotation * remaining;

            // 落位阶段的过冲，结束时间点自动归零。
            var overshootPhase = Mathf.Clamp01((progress - mArriveRatio) / Mathf.Max(0.01f, 1f - mArriveRatio));
            var overshoot = Mathf.Sin(overshootPhase * Mathf.PI) * mOvershoot;
            state.Scale *= 1f + overshoot;
            state.ScaleXY *= new Vector2(1f + overshoot * 0.6f, 1f - overshoot * 0.6f);

            state.Alpha *= Mathf.Clamp01(progress * 3f);
        }
    }
}
