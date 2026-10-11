using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GameFramework
{
    public sealed class UITextMarkupResult
    {
        public string Text;//去标签 后的文本
        public readonly List<UITextEffectRange> Ranges = new List<UITextEffectRange>();//标签对应的字符范围
    }

    /// <summary>
    /// 只解析框架自己的 fx 标签，其他 TMP 标签原样交回 TMP。
    /// </summary>
    public static class UITextMarkupParser
    {
        public static UITextMarkupResult Parse(string source)
        {
            var result = new UITextMarkupResult();//返回对象 彪悍文本 以及fx范围
            var output = new StringBuilder();//用于构造清理标签后的文本
            var openRanges = new Stack<UITextEffectRange>();///缓存 遇到开始标签，但是还没有遇到结束标签的范围 
            var characterCount = 0;//统计输出文本中的字符位置，不是字节数

            for (var i = 0; i < source.Length; i++)//处理原始字符串
            {   
                //***普通字符的处理***保留普通字符 并且计入输出位置
                if (source[i] != '<')
                {
                    output.Append(source[i]);
                    characterCount++;
                    continue;
                }
                //***遇到< , 寻找> ***，没有 >时  就当作普通字符
                var close = source.IndexOf('>', i);
                if (close < 0)
                {
                    output.Append(source[i]);
                    characterCount++;
                    continue;
                }
                //***提取标签内容***
                var tag = source.Substring(i + 1, close - i - 1).Trim();
                //**读到结束标签 并进行处理***
                if (tag == "/fx")
                {
                    if (openRanges.Count > 0)
                    {
                        var range = openRanges.Pop();//取出之前保存的范围 （读到< 但是没读到> 的缓存）
                        range.EndExclusive = characterCount;
                        var characterLength = Math.Max(0, range.EndExclusive - range.Start);
                        range.EffectDuration = 0.01f;
                        foreach (var effect in range.Effects)
                        {
                            range.EffectDuration = Math.Max(
                                range.EffectDuration, effect.GetDuration(characterLength));
                        }
                        range.Duration = range.Playback.GetTotalDuration(range.EffectDuration);
                        //获取到fx影响的最终范围  并加入结果
                        result.Ranges.Add(range);
                    }

                    i = close;
                    continue;
                }
                
                if (tag.StartsWith("fx=", StringComparison.OrdinalIgnoreCase))
                {
                    var range = new UITextEffectRange();
                    var expression = tag.Substring(3).Trim();
                    var parts = expression.Split(
                        new[] { ' ' },
                        StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (string.Equals(part, "loop", StringComparison.OrdinalIgnoreCase))
                        {
                            range.Playback.PlaybackMode = UITextEffectPlayback.Mode.Loop;
                            continue;
                        }

                        if (string.Equals(part, "once", StringComparison.OrdinalIgnoreCase))
                        {
                            range.Playback.PlaybackMode = UITextEffectPlayback.Mode.Once;
                            continue;
                        }

                        var equalsIndex = part.IndexOf('=');
                        if (equalsIndex > 0)
                        {
                            var option = part.Substring(0, equalsIndex);
                            var value = part.Substring(equalsIndex + 1);
                            if (string.Equals(option, "loop", StringComparison.OrdinalIgnoreCase) &&
                                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var loopCount))
                            {
                                range.Playback.PlaybackMode = UITextEffectPlayback.Mode.Loop;
                                range.Playback.LoopCount = loopCount;
                                continue;
                            }

                            if (string.Equals(option, "cycles", StringComparison.OrdinalIgnoreCase) &&
                                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles))
                            {
                                range.Playback.PlaybackMode = UITextEffectPlayback.Mode.Loop;
                                range.Playback.LoopCount = cycles;
                                continue;
                            }

                            if (string.Equals(option, "delay", StringComparison.OrdinalIgnoreCase) &&
                                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var delay))
                            {
                                range.Playback.Delay = Math.Max(0f, delay);
                                continue;
                            }
                        }

                        foreach (var id in part.Split(','))
                        {
                            var effects = UITextEffectLibrary.FindEffects(id);
                            if (effects == null)
                            {
                                continue;
                            }

                            foreach (var effect in effects)
                            {
                                if (effect != null)
                                {
                                    range.Effects.Add(effect);
                                }
                            }
                        }
                    }
                    //保存 特效影响的开始位置， 比如  获得了<fx=highlight_quest>普通</fx>
                    //range.Start = 3;
                    range.Start = characterCount;
                    openRanges.Push(range);//进入暂存 表示特效从 索引3 开始影响，等遇到</fx> 再设置end
                    i = close;
                    continue;
                }

                // 不是框架标签，例如 <color> 或 <size>，交回 TMP 处理。
                //经过上方解析后交给 TMP 的文本是：<color=#FFD45A>普通</color>
                output.Append(source, i, close - i + 1);
                i = close;
            }

            result.Text = output.ToString();
            return result;
        }
    }
}
