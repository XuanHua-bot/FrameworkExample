using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 高质感数字切片撕裂 (Digital Glitch Slice):
    /// 模拟专业 PV 剪辑中的 RGB 错位与水平/斜切断层。
    /// 采用泊松分布随机脉冲触发（非廉价线性跑表），
    /// 切片伴随快速的亚像素颤抖、高对比度荧光青/霓虹品红分离以及随机微错切。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Glitch Slice")]
    public sealed class UITextGlitchSliceEffect : UITextAnimationEffect
    {
        [Tooltip("故障发生的主频节拍")]
        [SerializeField, Range(2f, 25f)] private float mFrequency = 14f;

        [Tooltip("单次故障发生概率阈值")]
        [SerializeField, Range(0.1f, 0.8f)] private float mIntensity = 0.45f;

        [Tooltip("横向撕裂最大偏移")]
        [SerializeField, Range(5f, 40f)] private float mMaxSliceX = 26f;

        [Tooltip("纵向错位最大偏移")]
        [SerializeField, Range(0f, 15f)] private float mMaxSliceY = 8f;

        [Tooltip("斜切撕裂强度")]
        [SerializeField, Range(0f, 0.8f)] private float mMaxSkew = 0.45f;

        [Tooltip("顶端 RGB 分离色（数码青）")]
        [SerializeField] private Color mCyanShift = new Color(0.2f, 1f, 0.95f, 1f);

        [Tooltip("底端 RGB 分离色（荧光洋红）")]
        [SerializeField] private Color mMagentaShift = new Color(1f, 0.15f, 0.65f, 1f);

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var charIdx = context.CharacterIndex - context.RangeStart;
            var t = context.Elapsed * mFrequency;
            var step = Mathf.FloorToInt(t);

            // 构造强节奏的数码故障触发因子（非连续匀速抖，而是断续式的瞬时抽搐）
            var trigger = Hash01(step, 107);
            if (trigger > mIntensity)
            {
                return; // 绝大多数时间完全纯净稳定，对比才炸裂
            }

            // 以 2~3 个相邻字符为一个随机撕裂条带
            var band = (charIdx / 2) + step * 31;
            var isSlicing = Hash01(band, 211) < 0.65f;
            if (!isSlicing)
            {
                return;
            }

            var dirX = (Hash01(band, 317) > 0.5f) ? 1f : -1f;
            var amountX = (Hash01(band, 431) * 0.7f + 0.3f) * mMaxSliceX;
            var amountY = (Hash01(band, 523) * 2f - 1f) * mMaxSliceY;
            var skewVal = (Hash01(band, 619) * 2f - 1f) * mMaxSkew;

            state.Offset.x += dirX * amountX;
            state.Offset.y += amountY;
            state.Skew += skewVal;

            // 极致纯正的色度通道分离 (RGB Splitting)
            var alpha = state.Color.a / 255f;
            var top = Color.Lerp((Color)state.Color, mCyanShift, 0.85f);
            var bot = Color.Lerp((Color)state.Color, mMagentaShift, 0.85f);
            top.a = alpha;
            bot.a = alpha;

            state.Color = top;
            state.ColorBottom = bot;
            state.Gradient = true;

            // 偶尔抽帧微透
            if (Hash01(band, 701) < 0.25f)
            {
                state.Alpha *= 0.6f;
            }
        }
    }
}
