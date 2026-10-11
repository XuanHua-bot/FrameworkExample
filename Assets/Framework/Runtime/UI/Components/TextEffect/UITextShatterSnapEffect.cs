using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 碎裂重组（粒子化）：字符先以随机方向和随机旋转散落在四周，并按随机时机
    /// 一片片"抽"回原位，归位瞬间横向挤压并过冲一下；组装过程中带有偶发的亮度闪烁，
    /// 形成数码粒子聚拢的质感。
    /// （已回退至最干净的第一版手感，仅修复了多行文本索引偏移的 bug）
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Shatter Snap")]
    public sealed class UITextShatterSnapEffect : UITextAnimationEffect
    {
        [SerializeField, Min(0f)] private float mCharacterDelay = 0.05f;
        [SerializeField, Range(0f, 1f)] private float mTimingJitter = 0.6f;
        [SerializeField, Min(0f)] private float mScatterRadius = 64f;
        [SerializeField, Range(0f, 360f)] private float mScatterRotation = 220f;
        [SerializeField, Range(0.4f, 0.95f)] private float mSnapRatio = 0.72f;
        [SerializeField, Range(1f, 1.6f)] private float mSnapScale = 1.22f;
        [SerializeField, Range(0f, 0.12f)] private float mSnapSquash = 0.1f;

        public override float GetDuration(int characterCount)
        {
            var stagger = Mathf.Max(0, characterCount - 1) * mCharacterDelay * (1f + mTimingJitter);
            return GetDuration() + stagger;
        }

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var order = context.CharacterIndex - context.RangeStart;
            // 随机时机：让字符不是从头到尾依次归位，而是像粒子一样零散地聚拢。
            var stagger = order * mCharacterDelay * Mathf.Lerp(1f - mTimingJitter, 1f + mTimingJitter, Hash01(order, 31));
            var progress = Evaluate(context.Elapsed - stagger);
            var assemble = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / mSnapRatio));
            var scatter = 1f - assemble;

            var angle = Hash01(order, 32) * Mathf.PI * 2f;
            var radius = mScatterRadius * Mathf.Lerp(0.5f, 1f, Hash01(order, 33));
            state.Offset += new Vector3(Mathf.Cos(angle) * radius * scatter, Mathf.Sin(angle) * radius * scatter, 0f);

            var spin = Hash01(order, 34) < 0.5f ? -1f : 1f;
            state.Rotation += spin * mScatterRotation * scatter;

            // 归位瞬间：过冲缩放 + 横向挤压，做出"啪"的吸附感。
            var snapPhase = Mathf.Clamp01((progress - mSnapRatio) / Mathf.Max(0.01f, 1f - mSnapRatio));
            var snap = Mathf.Sin(snapPhase * Mathf.PI);
            state.Scale *= Mathf.Lerp(1f, mSnapScale, snap);
            state.ScaleXY *= new Vector2(1f + mSnapSquash * snap, 1f - mSnapSquash * snap);

            state.Alpha *= Mathf.Clamp01(progress * 3f);

            // 组装过程中的偶发闪烁：散落状态下按时间片随机变暗，归位后停止。
            if (scatter > 0.02f)
            {
                var tick = Mathf.FloorToInt(context.Elapsed * 24f);
                if (Hash01(order, 40 + tick) < 0.3f)
                {
                    state.Alpha *= 0.3f;
                }
            }
        }
    }
}
