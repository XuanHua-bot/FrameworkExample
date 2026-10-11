using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 蓄力爆字：先"憋住"——整段文字缩小、左右交替倾斜、下沉并压暗，
    /// 然后从右向左炸开：起爆瞬间全词过冲放大并弹起，随后一道冲击波沿字扫过，
    /// 波心字符被大幅放大、抬起并甩转，最后回落到原始状态。
    /// 运动量（缩放/倾斜/位移）是主要表现手段，颜色只负责确认爆点。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Charge Strike")]
    public sealed class UITextChargeStrikeEffect : UITextAnimationEffect
    {
        [SerializeField, Range(0.1f, 0.9f)] private float mChargeRatio = 0.5f;
        [SerializeField] private Color mChargeColor = new Color(0.45f, 0.45f, 0.52f, 1f);
        [SerializeField, Range(0.5f, 1f)] private float mChargeScale = 0.8f;
        [SerializeField, Range(0f, 25f)] private float mChargeTilt = 9f;
        [SerializeField, Min(0f)] private float mChargeSink = 5f;
        [SerializeField] private Color mFlashColor = new Color(1f, 0.94f, 0.72f, 1f);
        [SerializeField, Range(1f, 2f)] private float mBoomScale = 1.35f;
        [SerializeField, Min(0f)] private float mBoomLift = 11f;
        [SerializeField, Range(0.03f, 0.3f)] private float mWaveWidth = 0.09f;
        [SerializeField, Range(1f, 2f)] private float mStrikeScale = 1.5f;
        [SerializeField, Min(0f)] private float mStrikeLift = 16f;
        [SerializeField, Range(0f, 30f)] private float mStrikeTilt = 14f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var phase = Mathf.Clamp01(context.Elapsed / Mathf.Max(0.01f, context.Duration));
            if (phase <= 0f)
            {
                return;
            }

            // 左右交替的倾斜方向：让蓄力的"拧紧"能看出来。
            var direction = (context.CharacterIndex & 1) == 0 ? 1f : -1f;
            var color = (Color)state.Color;

            if (phase < mChargeRatio)
            {
                var charge = Mathf.SmoothStep(0f, 1f, phase / mChargeRatio);
                state.Scale *= Mathf.Lerp(1f, mChargeScale, charge);
                state.Rotation += direction * mChargeTilt * charge;
                state.Offset.y -= mChargeSink * charge;
                color = Color.Lerp(color, mChargeColor, charge);
            }
            else
            {
                var strike = (phase - mChargeRatio) / Mathf.Max(0.01f, 1f - mChargeRatio);
                // 最后 20% 时间统一回落，保证一次性效果结束时回到原始状态。
                // Mathf.SmoothStep 返回 from~to 之间的值，这里先用 InverseLerp 归一化。
                var settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, strike));
                var release = 1f - settle;

                // 蓄力状态在起爆瞬间快速释放。
                var chargeHold = Mathf.Clamp01(1f - strike / 0.15f);
                state.Scale *= Mathf.Lerp(1f, mChargeScale, chargeHold);
                state.Rotation += direction * mChargeTilt * chargeHold;
                state.Offset.y -= mChargeSink * chargeHold;
                color = Color.Lerp(color, mChargeColor, chargeHold);

                // 起爆：全词过冲放大并弹起，颜色冲亮。
                var boom = Mathf.Clamp01(1f - strike / 0.12f) * release;
                state.Scale *= Mathf.Lerp(1f, mBoomScale, boom);
                state.Offset.y += mBoomLift * boom;

                // 冲击波从右端推到左端，波心字符被甩转、抬起、放大。
                var wavePosition = 1f - Mathf.Clamp01(strike / 0.8f);
                var distance = context.RangeProgress - wavePosition;
                var impact = Mathf.Exp(-(distance * distance) / (2f * mWaveWidth * mWaveWidth)) * release;
                state.Scale *= Mathf.Lerp(1f, mStrikeScale, impact);
                state.Offset.y += mStrikeLift * impact;
                state.Rotation += direction * mStrikeTilt * impact;

                color = Color.Lerp(color, mFlashColor, Mathf.Max(boom, impact));
            }

            color.a = state.Color.a / 255f;
            state.Color = color;
        }
    }
}
