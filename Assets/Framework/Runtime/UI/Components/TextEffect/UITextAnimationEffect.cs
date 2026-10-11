using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 单个字符在当前帧的表现状态。（计算中的临时数据 每个字符一个状态，多个effect使用同一份状态）
    /// 多个效果会按照列表顺序依次修改这个状态。
    /// </summary>
    public struct UITextCharacterState
    {
        public bool Visible;
        public float Alpha;
        public float Scale;

        /// <summary>
        /// 非等比缩放，用于挤压/拉伸（弹跳落位、翻转入场等）。默认 (1, 1)。
        /// 与 Scale 相乘后一起作用到字符四边形的顶点上。
        /// </summary>
        public Vector2 ScaleXY;

        /// <summary>
        /// 水平斜切：按顶点相对字符中心的高度横向错开。
        /// 用来做斜体/梯形/数码切片撕裂的错觉。默认 0。
        /// </summary>
        public float Skew;

        /// <summary>
        /// 是否启用上下双色渐变。为 true 时下半部分顶点使用 ColorBottom。
        /// </summary>
        public bool Gradient;

        /// <summary>
        /// 渐变的下半部分颜色（仅 Gradient 为 true 时生效）。
        /// 效果只改 Color 时应保持 Gradient 为 false，避免出现意外的渐层。
        /// </summary>
        public Color32 ColorBottom;

        public float Rotation;
        public Vector3 Offset;
        public Color32 Color;
    }

    /// <summary>
    /// 提供给文字效果的运行时上下文。
    /// </summary>
    public readonly struct UITextAnimationContext
    {
        public readonly int CharacterIndex;
        public readonly int RangeStart;
        public readonly int RangeEnd;
        public readonly float Elapsed;//经过时间
        public readonly float Duration;
        public readonly int PunctuationCount;

        /// <summary>当前字符基线中心（文本局部坐标）。按位置计算涟漪、路径排布时使用。</summary>
        public readonly Vector2 CharacterCenter;

        /// <summary>整段文本的中心（文本局部坐标）。</summary>
        public readonly Vector2 TextCenter;

        /// <summary>整段文本的尺寸。用于把字符位置归一化成 -1~1 之类的比例。</summary>
        public readonly Vector2 TextSize;

        /// <summary>当前字符所在行号（从 0 开始）。</summary>
        public readonly int LineIndex;

        public UITextAnimationContext(int characterIndex, int rangeStart, int rangeEnd, float elapsed, float duration,
            int punctuationCount = 0, Vector2 characterCenter = default, Vector2 textCenter = default,
            Vector2 textSize = default, int lineIndex = 0)
        {
            CharacterIndex = characterIndex;
            RangeStart = rangeStart;
            RangeEnd = rangeEnd;
            Elapsed = elapsed;
            Duration = duration;
            PunctuationCount = punctuationCount;
            CharacterCenter = characterCenter;
            TextCenter = textCenter;
            TextSize = textSize;
            LineIndex = lineIndex;
        }

        public float RangeProgress
        {
            get
            {
                var length = Mathf.Max(1, RangeEnd - RangeStart);
                return Mathf.Clamp01((CharacterIndex - RangeStart) / (float)length);//[0,1]范围， 表示当前字符在当前效果范围中的位置
            }
        }

        /// <summary>当前字符相对文本中心的水平位置，归一化到 -1~1（用于沿路径/环形排布）。</summary>
        public float HorizontalRatio
        {
            get
            {
                var halfWidth = Mathf.Max(1f, TextSize.x * 0.5f);
                return Mathf.Clamp((CharacterCenter.x - TextCenter.x) / halfWidth, -1f, 1f);
            }
        }
    }

    /// <summary>
    /// 所有文字效果的扩展基类。
    /// 自定义效果只需要继承此类并实现 Apply。
    /// </summary>
    public abstract class UITextAnimationEffect : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float mDuration = 0.2f;
        [SerializeField] private AnimationCurve mCurve = null;

        protected float Duration => mDuration;

        /// <summary>
        /// 返回该效果自己的播放时长。
        /// 范围播放时会用所有效果中的最大时长，避免不同效果被一个全局时长强行截断。
        /// </summary>
        public float GetDuration()
        {
            return mDuration;
        }

        /// <summary>
        /// 返回效果作用于指定字符数时的完整时长。
        /// 需要逐字符延迟的效果可以重写此方法，把最后一个字符的延迟计算进去。
        /// </summary>
        public virtual float GetDuration(int characterCount)
        {
            return mDuration;
        }

        public virtual float GetDuration(int characterCount, int punctuationCount)
        {
            return GetDuration(characterCount);
        }

        /// <summary>
        /// 稳定的逐字符伪随机数（0~1）。同一个字符 + 同一个通道永远得到同一个值，
        /// 这样"随机方向/随机时机"的效果不会每帧乱跳；需要随时间变化的随机再叠加时间通道。
        /// </summary>
        protected static float Hash01(int index, int channel)
        {
            var value = Mathf.Sin(index * 12.9898f + channel * 78.233f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }

        /// <summary>
        /// 把经过的描述 映射至0~1，用配置曲线改变速度
        /// aka  经过的时间=>动画的进度
        /// </summary>
        protected float Evaluate(float elapsed)
        {
            var progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, mDuration));
            //没有配置曲线时，直接返回线性进度 progress
            //配置了曲线时，用曲线重新计算进度
            // Unity 序列化的空 AnimationCurve 不是 null，但没有关键帧时不能参与计算。
            return mCurve == null || mCurve.length == 0
                ? progress
                : mCurve.Evaluate(progress);
        }

        /// <summary>
        /// 修改当前字符的表现状态。
        /// </summary>
        public abstract void Apply(ref UITextCharacterState state, UITextAnimationContext context);
    }

}
