using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 扫描锁定：一条读取头从左到右扫过文字。
    /// 未读取的字符压暗并轻微缩小（"未激活"），读取头扫过时字符被纯白点亮、弹起、放大，
    /// 之后驻留在明亮的锁定色。循环播放时会反复重新扫描；出场默认不参与。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Pulse Scanner")]
    public sealed class UITextPulseScannerEffect : UITextAnimationEffect
    {
        [SerializeField] private Color mDimColor = new Color(0.34f, 0.39f, 0.5f, 1f);
        [SerializeField, Range(0f, 1f)] private float mDimAmount = 0.9f;
        [SerializeField, Range(0f, 0.3f)] private float mDimShrink = 0.08f;
        [SerializeField] private Color mLockedColor = new Color(0.88f, 0.95f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float mLockedAmount = 0.5f;
        [SerializeField, Range(0.02f, 0.25f)] private float mHeadWidth = 0.08f;
        [SerializeField, Range(0f, 1f)] private float mPopScale = 0.35f;
        [SerializeField, Min(0f)] private float mPopOffset = 8f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var progress = Mathf.Clamp01(context.Elapsed / Mathf.Max(0.01f, context.Duration));
            var delta = context.RangeProgress - progress;
            var color = (Color)state.Color;

            if (delta > 0f)
            {
                // 还没被读取：压暗 + 轻微缩小，接近读取头时逐渐过渡。
                // 注意 Mathf.SmoothStep 返回的是 from~to 之间的值，这里必须先把距离归一化。
                var ramp = Mathf.Clamp01(delta / Mathf.Max(0.001f, mHeadWidth * 2f));
                var edge = Mathf.SmoothStep(0f, 1f, ramp);
                color = Color.Lerp(color, mDimColor, edge * mDimAmount);
                state.Alpha *= 1f - 0.2f * edge;
                state.Scale *= 1f - mDimShrink * edge;
            }
            else
            {
                // 已读取：锁定色驻留，读取头刚经过时纯白点亮并弹起。
                var behind = -delta;
                var spark = Mathf.Clamp01(1f - behind / Mathf.Max(0.001f, mHeadWidth));
                spark *= spark;

                color = Color.Lerp(color, mLockedColor, mLockedAmount);
                color = Color.Lerp(color, Color.white, spark);
                state.Scale *= 1f + mPopScale * spark;
                state.Offset.y += mPopOffset * spark;
            }

            color.a = state.Color.a / 255f;
            state.Color = color;
        }
    }
}
