# UITextAnimator 文字动画系统

## 一、目标

本阶段实现一个基于 TMP 顶点的文字动画组件，参考 SKCell 的 `SKTextAnimator` 和 Unity Text Animator 的行内效果思路，但不直接接入第三方文字动画插件。

最终可以这样写文本：

```text
我是一个 <fx=highlight_quest>普通</fx> 文本
```

不使用连续嵌套的结束标签，例如 `</scale></fade></shake>`。一个 `fx` 标签代表一组作用于同一文字范围的效果。

## 二、文件位置

下面的脚本都放在框架运行时代码目录中，不要放到 `Assets/Example`：

```text
Assets/Framework/Runtime/UI/Components/TextEffect/
├── UITextAnimationEffect.cs   // 效果基类、字符状态和动画上下文
├── UITextEffectBinding.cs      // 文本 ID 到效果资源的绑定
├── UITextEffectRange.cs        // 一段文字对应的字符范围和效果集合
├── UITextMarkupParser.cs       // 解析 <fx=...> 标签
└── UITextAnimator.cs           // TMP 网格动画和播放生命周期
```

文件处理规则：

- `UITextAnimationEffect.cs`：保留或修改现有文件，不要重复创建同名类。
- `UITextAnimator.cs`：保留或修改现有文件，不要在页面 Controller 中复制一套动画循环。
- `UITextEffectBinding.cs`、`UITextEffectRange.cs`、`UITextMarkupParser.cs`：如果项目中还没有，就在上面的目录中分别新建。
- 每个公共类型使用与文件相同的文件名，避免 Unity 编译后难以定位错误。

运行时效果：

- `普通` 两个字抖动。
- `普通` 两个字逐渐显示。
- `普通` 两个字放大出现。
- 其他文字保持静止。
- TMP 原生标签，例如 `<color>`、`<size>`，继续由 TMP 处理。

示例：

```text
我是一个 <color=#FFD45A><fx=highlight_quest>普通</fx></color> 文本
```

常用的组合可以注册成一个预设：

```text
我是一个 <fx=highlight>普通</fx> 文本
```

`highlight` 由 Inspector 配置为 `shake + fade + scale`。业务文本只使用预设 ID，不需要重复写效果组合。

## 三、和框架四层的关系

`UITextAnimator` 是 UI 表现层组件，不属于游戏业务系统。

```text
Controller / UIPage
    ↓ 调用 Play(text)
UITextAnimator
    ↓ 修改 TMP 顶点
TMP_Text
```

组件的职责：

- 解析自定义文字标签。
- 记录每个效果作用的字符范围。
- 修改 TMP 字符顶点。
- 管理播放、暂停、跳过、停止和完成事件。

组件不负责：

- 发送网络请求。
- 处理服务器错误。
- 查询 Model 或 System。
- 决定业务文本内容。

页面 Controller 可以获取 `UITextAnimator`，但业务状态变化仍然通过 Command、System 和 Event 处理。

## 四、效果扩展方式

文字效果拆分成独立的 `ScriptableObject`，`UITextAnimator` 不写死具体效果。

```text
UITextAnimator
    ├── TypewriterEffect
    ├── FadeEffect
    ├── ScaleEffect
    └── 自定义 Effect
```

自定义效果只需要：

1. 继承 `UITextAnimationEffect`。
2. 实现 `Apply`。
3. 创建一个 Effect 资源。
4. 在文字动画器的效果表中注册 ID。

效果 ID 表示“改变什么”，不要把 `FastFade`、`SlowFade`、`PopIn` 直接当作新的效果类型。它们应该是已有效果的不同配置：

```text
fade + 短时长 + EaseOut       = FastFade 的表现
scale + 起始值 0.7 + BackOut   = PopIn 的表现
```

核心接口：

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextAnimationEffect.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace GameFramework
{
    /// <summary>
    /// 单个字符当前帧的可变表现状态。
    /// </summary>
    public struct UITextCharacterState
    {
        public bool Visible;
        public float Alpha;
        public float Scale;
        public Vector3 Offset;
        public Color32 Color;
    }

    /// <summary>
    /// 当前字符对应的动画上下文。
    /// </summary>
    public readonly struct UITextAnimationContext
    {
        public readonly int CharacterIndex;
        public readonly int RangeStart;
        public readonly int RangeEnd;
        public readonly float Elapsed;
        public readonly float Duration;

        public UITextAnimationContext(
            int characterIndex,
            int rangeStart,
            int rangeEnd,
            float elapsed,
            float duration)
        {
            CharacterIndex = characterIndex;
            RangeStart = rangeStart;
            RangeEnd = rangeEnd;
            Elapsed = elapsed;
            Duration = duration;
        }

        public float RangeProgress
        {
            get
            {
                var length = Mathf.Max(1, RangeEnd - RangeStart);
                return Mathf.Clamp01((CharacterIndex - RangeStart) / (float)length);
            }
        }
    }

    /// <summary>
    /// 一个文字效果的扩展基类。
    /// </summary>
    public abstract class UITextAnimationEffect : ScriptableObject
    {
        /// <summary>
        /// 修改当前字符这一帧的表现状态。
        /// </summary>
        public abstract void Apply(
            ref UITextCharacterState state,
            UITextAnimationContext context);
    }
}
```

## 五、效果注册表

为了让标签不写死在代码中，使用一个 ID 和 Effect 资源的绑定：

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextEffectBinding.cs`

```csharp
using UnityEngine;
using System.Collections.Generic;

namespace GameFramework
{
    /// <summary>
    /// 把文本标签中的 ID 映射到一个具体效果资源。
    /// </summary>
    [System.Serializable]
    public sealed class UITextEffectBinding
    {
        // 文本标签中的 ID，例如 <fx=shake> 中的 shake。
        [Tooltip("文本标签使用的 ID，例如 shake、fade、scale")]
        public string Id;

        // 具体效果资源由 Inspector 绑定，文本本身不创建 ScriptableObject。
        // 同一个 ID 可以组合多个效果，执行顺序由列表顺序决定。
        public List<UITextAnimationEffect> Effects =
            new List<UITextAnimationEffect>();
    }
}
```

创建项目级效果库：

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextEffectLibrary.cs`

在 Unity 中创建：

```text
Create → Game Framework → UI Text Effects → Library
```

效果库放在：

```text
Assets/Framework/Resources/UI/TextEffects/UITextEffectLibrary.asset
```

在这个共享库中配置：

```text
UITextEffectLibrary
    Id: highlight_quest   Effects: FadeEffect, ScaleEffect
    Id: highlight_map     Effects: ShakeEffect, FloatEffect
    Id: warning            Effects: ShakeEffect
```

当前项目已经在这个资源库中配置了以下组合 ID：

```text
highlight        → FadeIn + ScaleIn + Shake
highlight_quest  → FadeIn + ScaleIn
highlight_map    → Shake + Float
fade             → FadeIn
scale            → ScaleIn
shake            → Shake
float            → Float
warning          → Shake
typewriter       → Typewriter
```

这些 ID 全部来自 `UITextEffectLibrary.asset`。以后新增组合时，只需在该资产中增加一条绑定，并把需要的 Effect 资源按顺序拖入 `Effects`。

之后文本中的：

```text
<fx=highlight_quest>普通</fx>
```

就会映射到这三个 Effect 资源。

## 六、标签解析规则

第一版只解析框架自己的 `<fx=...>...</fx>`，其余标签原样保留给 TMP。

```text
自定义标签：<fx=highlight_quest>文字</fx>
循环标签：<fx=highlight_map loop>文字</fx>
TMP 标签：<color=#FFCC00>文字</color>
```

第一版只支持这一种自定义标签：

```text
<fx=效果ID>文字</fx>
<fx=效果ID once>文字</fx>
<fx=效果ID loop>文字</fx>
```

标签内部使用逗号组合多个已注册效果：

```text
<fx=highlight_quest>文字</fx>
```

不在文本中直接写 `AnimationCurve`。曲线、默认时长和延迟在 Effect 资源中配置；播放一次还是循环通过标签后的 `once`、`loop` 修饰词指定。这样同一个 Effect 不需要复制成两份状态。

解析流程：

```text
原始文本
    ↓
扫描 <fx=...> 和 </fx>
    ↓
删除自定义标签
    ↓
记录开始字符和结束字符
    ↓
保留 <color>、<size> 等 TMP 标签
    ↓
得到 TMP 最终文本和效果范围表
```

范围数据可以定义为：

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextEffectRange.cs`

```csharp
using System.Collections.Generic;

namespace GameFramework
{
    /// <summary>
    /// 一组效果作用的字符范围，EndExclusive 不包含在范围内。
    /// </summary>
    public sealed class UITextEffectRange
    {
        // 去除自定义标签后，范围包含的第一个字符序号。
        public int Start;

        // 结束位置不包含在范围内，便于使用 index < EndExclusive 判断。
        public int EndExclusive;

        // 同一个范围可以叠加多个效果，执行顺序由列表顺序决定。
        public readonly List<UITextAnimationEffect> Effects =
            new List<UITextAnimationEffect>();
    }
}
```

例如：

```text
我是一个 <fx=highlight_quest>普通</fx> 文本
```

会生成：

```text
Text: 我是一个 普通 文本
Range:
    Start: 5
    EndExclusive: 7
    Effects: shake, fade, scale
```

## 七、解析器的最小实现

解析器只负责拆出纯文本和范围，不负责修改 TMP 网格。

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextMarkupParser.cs`

```csharp
using System.Collections.Generic;
using System.Text;

namespace GameFramework
{
    public sealed class UITextMarkupResult
    {
        public string Text;
        public readonly List<UITextEffectRange> Ranges =
            new List<UITextEffectRange>();
    }

    public static class UITextMarkupParser
    {
        public static UITextMarkupResult Parse(string source)
        {
            // result.Text 是交给 TMP 的最终文本；Ranges 保存自定义动画范围。
            var result = new UITextMarkupResult();
            var output = new StringBuilder();

            // 自定义标签允许成对出现，因此使用栈保存尚未闭合的范围。
            var openRanges = new Stack<UITextEffectRange>();

            // 这里记录去除自定义标签后的字符序号。
            // TMP 的 <color>、<size> 等标签不应该占用动画范围索引。
            var characterCount = 0;

            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] != '<')
                {
                    // 普通字符直接保留，并计入范围索引。
                    output.Append(source[i]);
                    characterCount++;
                    continue;
                }

                var close = source.IndexOf('>', i);
                if (close < 0)
                {
                    // 标签不完整时，把当前字符当普通文本保留。
                    output.Append(source[i]);
                    continue;
                }

                var tag = source.Substring(i + 1, close - i - 1);
                if (tag == "/fx")
                {
                    if (openRanges.Count > 0)
                    {
                        // 关闭最近打开的范围，EndExclusive 不包含当前结束位置。
                        var range = openRanges.Pop();
                        range.EndExclusive = characterCount;
                        result.Ranges.Add(range);
                    }

                    i = close;
                    continue;
                }

                if (tag.StartsWith("fx="))
                {
                    // 一个 fx 标签可以包含多个以逗号分隔的效果 ID。
                    var range = new UITextEffectRange
                    {
                        Start = characterCount
                    };

                    var ids = tag.Substring(3).Split(',');
                    foreach (var id in ids)
                    {
                        var effects = UITextEffectLibrary.FindEffects(id);
                        if (effects != null)
                        {
                            foreach (var effect in effects)
                            {
                                if (effect != null)
                                {
                                    // 未注册的 ID 被忽略，不让错误文本使整个 UI 崩溃。
                                    range.Effects.Add(effect);
                                }
                            }
                        }
                    }

                    openRanges.Push(range);
                    i = close;
                    continue;
                }

                // 不是框架标签，交回 TMP 处理，例如 <color> 和 <size>。
                output.Append(source, i, close - i + 1);
                i = close;
            }

            result.Text = output.ToString();
            return result;
        }
    }
}
```

这里的 `Start` 和 `EndExclusive` 使用去除自定义标签后的字符序号，而不是字符串下标。这样 TMP 的 `<color>`、`<size>` 等标签不会污染动画范围。

上面的最小解析器适合第一轮实现。后续需要补充：

- 标签不闭合时的错误处理。
- 预设参数覆盖，例如在调用 API 时临时覆盖持续时间。
- 标签参数，例如 `<fx=shake(2)>`，作为后续可选功能。
- Unicode 字符和 TMP 字符索引的映射。
- 富文本标签内部不应该被计入动画字符范围。

## 八、UITextAnimator 的职责调整

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextAnimator.cs`

当前 `UITextAnimator` 的全局 `Effects` 列表可以保留，作为整段文本效果：

```text
Global Effects
    整段文字都应用

Inline Effects
    只有 <fx=...> 范围内的文字应用
```

逐帧处理时：

```text
ForceMeshUpdate
    ↓
读取 TMP_TextInfo
    ↓
找到当前字符对应的范围
    ↓
先应用全局效果
    ↓
再应用当前范围的效果
    ↓
写回四个顶点和颜色
    ↓
UpdateVertexData
```

建议公开的调用接口：

```csharp
public void Play(string text);
public void Pause();
public void Resume();
public void CompleteImmediately();
public void Stop(bool reset);
public bool IsPlaying { get; }
public event System.Action Completed;
```

Controller 只调用这些接口，不直接操作 `TMP_TextInfo`。

## 九、时长、播放和曲线

文字效果需要区分四个概念：

```text
Effect      改变透明度、位置、缩放或旋转
Playback    控制这段文字是播放一次还是循环
Curve       控制变化节奏
```

全局库中只配置一次效果：

```text
Id: highlight
Effects: ShakeEffect, FadeInEffect, ScaleInEffect
```

调用方只写：

```csharp
mAnimator.Play("我是一个 <fx=highlight>普通</fx> 文本");
```

默认播放一次。如果只想让这一次循环播放，可以在标签后增加播放修饰词：

```csharp
mAnimator.Play("任务：<fx=highlight loop>目标</fx>");
mAnimator.Play("提示：<fx=highlight once>目标</fx>");
```

两种写法仍然使用同一个 `highlight` 和同一组 Effect，只改变本次范围的播放模式。

## 十、和 DOTween 的边界

字符顶点动画不为每个字符创建一个 DOTween。原因是长文本会产生大量 Tween 实例，且每帧仍然需要修改 TMP 顶点。

当前分工：

```text
UITextAnimator：逐字符顶点、范围、播放进度
DOTween：Tooltip 面板淡入、整体缩放、整体移动
```

如果某个自定义效果需要缓动曲线，可以在 Effect 资源中使用 `AnimationCurve`；不需要让每个字符都创建 Tween。

## 十一、Controller 使用示例

示例文件：`Assets/Example/Scripts/ExampleMessageController.cs`

```csharp
using UnityEngine;

namespace GameFramework.Examples
{
    public sealed class ExampleMessageController : MonoBehaviour
    {
        [SerializeField] private GameFramework.UITextAnimator mAnimator;

        public void ShowMessage(string message)
        {
            // 文本内容来自业务层传入，动画组件只负责表现。
            mAnimator.Play(message);
        }
    }
}
```

页面脚本不需要知道 `shake` 如何修改顶点，也不需要判断服务器错误类型。网络错误、业务失败消息和文本内容仍由上层 System/Command 决定。

## 十二、实现顺序

1. 保留现有 `UITextAnimationEffect` 和基础效果。
2. 增加 `UITextEffectBinding`。
3. 增加 `UITextMarkupParser`。
4. 修改 `UITextAnimator`，支持全局效果和行内效果。
5. 加入 `Pause`、`Resume`、`CompleteImmediately`。
6. 处理 TMP 字符索引、空格、换行和富文本标签。
7. 用 Tooltip 和 Notification 组件接入。
8. 最后再增加调用级参数覆盖，不把复杂参数强行塞入业务文本。

## 十三、暂不实现的内容

- 不接入 Text Animator 或 TMPEffects 插件。
- 不把网络请求写入 `UITextAnimator`。
- 不让 Effect 直接获取 System 或 Model。
- 不为每个字符创建 GameObject。
- 不在第一轮加入完整的文本脚本语言。
- 不使用连续嵌套标签作为主要写法。

第一轮的完成标准是：

```text
同一段 TMP 文本
    可以保留颜色和字号
    可以用 <fx=...> 标记局部范围
    一个范围可以叠加多个效果
    自定义效果可以通过继承基类加入
    页面 Controller 只调用 Play/Stop/Complete
```
