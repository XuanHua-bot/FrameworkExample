using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 高反差电浆扫光光束 (Chromatic Plasma Beam):
    /// 解决在深色背景上白字"扫光看不清"的痛点：
    /// 1. 扫光本体注入高纯度发光金顶与青电底，并在光核正中附带强烈的+14px 弹跳抬升；
    /// 2. 紧随光束后方拖着一道 55% 强度的深暗裁切刀锋（高动态范围对比度反差），
    /// 产生极其凌厉清晰的"刀锋划过"质感。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Shine Sweep")]
    public sealed class UITextShineSweepEffect : UITextAnimationEffect
    {
        [Tooltip("扫光循环周期（秒）")]
        [SerializeField, Min(0.2f)] private float mCycleTime = 1.35f;

        [Tooltip("扫光光束核心宽度")]
        [SerializeField, Range(0.05f, 0.4f)] private float mBeamWidth = 0.18f;

        [Tooltip("扫光顶端核心色彩（荧光亮金）")]
        [SerializeField] private Color mBeamTopColor = new Color(1f, 0.92f, 0.35f, 1f);

        [Tooltip("扫光底端伴随色彩（霓虹冷青）")]
        [SerializeField] private Color mBeamBottomColor = new Color(0.25f, 0.95f, 1f, 1f);

        [Tooltip("光束扫过时的垂直跃动高度")]
        [SerializeField, Range(2f, 25f)] private float mLift = 14f;

        [Tooltip("光束扫过时的纵向伸缩形变")]
        [SerializeField, Range(0f, 0.6f)] private float mStretch = 0.35f;

        [Tooltip("光束前沿的高反差暗影剪刀边深度")]
        [SerializeField, Range(0f, 1f)] private float mDarkBlade = 0.55f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var duration = Mathf.Max(0.01f, mCycleTime);
            var rawT = Mathf.Repeat(context.Elapsed / duration, 1f);

            var activeRatio = 0.78f;
            if (rawT > activeRatio)
            {
                return; // 停顿期 100% 保持清晰纯白字
            }

            var sweepProgress = rawT / activeRatio;
            var sweepPos = Mathf.Lerp(-mBeamWidth * 2f, 1f + mBeamWidth * 2f, sweepProgress);

            var diff = context.RangeProgress - sweepPos;
            var dist = Mathf.Abs(diff);

            if (dist > mBeamWidth * 2.8f)
            {
                return;
            }

            // 高斯能量光束强度
            var intensity = Mathf.Exp(-(dist * dist) / (2f * mBeamWidth * mBeamWidth));
            var smoothIntensity = intensity * intensity * (3f - 2f * intensity);

            // 紧随光束的暗影刀口（产生极致明暗对比）
            var shadowDist = diff + mBeamWidth * 1.1f;
            var shadow = Mathf.Exp(-(shadowDist * shadowDist) / (2f * (mBeamWidth * 0.9f) * (mBeamWidth * 0.9f))) * mDarkBlade;

            var alpha = state.Color.a / 255f;
            var baseColor = (Color)state.Color * (1f - shadow);

            // 注入双色电浆
            var top = Color.Lerp(baseColor, mBeamTopColor, smoothIntensity);
            var bot = Color.Lerp(baseColor, mBeamBottomColor, smoothIntensity);
            top.a = alpha;
            bot.a = alpha;

            state.Color = top;
            state.ColorBottom = bot;
            state.Gradient = true;

            // 动态垂直抬升与挤压形变
            state.Offset.y += mLift * smoothIntensity;
            state.ScaleXY *= new Vector2(1f - mStretch * 0.45f * smoothIntensity, 1f + mStretch * smoothIntensity);
        }
    }
}
