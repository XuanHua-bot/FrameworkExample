using System.Collections.Generic;

namespace GameFramework
{
    /// <summary>
    /// 一组效果作用的字符范围。
    /// 确定某个特效作用于哪些字符索引。
    /// 
    /// EndExclusive 不包含在范围内。
    /// 使用左闭右开区间  是为了减少出现少减或多减一的问题
    /// 比如： 获 ，得， 空格， 普， 通， 空格， 物 ， 品 （0~7）
    /// Start = 3
    /// EndExclusive = 5
    /// length = 5 - 3 = 2
    /// 这正好表示索引 3、4 两个字符。
    /// </summary>
    public sealed class UITextEffectRange
    {
        public int Start;
        public int EndExclusive;
        public readonly List<UITextAnimationEffect> Effects = new List<UITextAnimationEffect>();
        public UITextEffectPlayback Playback = new UITextEffectPlayback();
        public float EffectDuration = 0.01f;
        public float Duration = 0.01f;

        public bool Contains(int characterIndex)
        {
            return characterIndex >= Start && characterIndex < EndExclusive;
        }
    }
}
