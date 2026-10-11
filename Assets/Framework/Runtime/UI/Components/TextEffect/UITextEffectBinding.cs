using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 把文本标签中的 ID 映射到一个或多个文字效果。
    /// </summary>
    [Serializable]
    public sealed class UITextEffectBinding
    {
        [Tooltip("文本标签使用的 ID，例如 shake、fade、scale 或 highlight")]
        public string Id;

        [Tooltip("该 ID 对应的效果资源列表，执行顺序就是列表顺序（每帧一次执行）")]
        public List<UITextAnimationEffect> Effects = new List<UITextAnimationEffect>();

    }
}
