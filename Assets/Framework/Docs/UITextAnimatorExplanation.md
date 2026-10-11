# UITextAnimator 源码解释

本文采用“源码片段 → 紧接着解释”的方式编写。阅读时不需要在代码和说明之间来回跳转。所有示例均对应当前目录：

```text
Assets/Framework/Runtime/UI/Components/TextEffect/
```

系统想解决的问题很简单：页面只提供一段文本，框架负责识别局部效果，并把效果写到 TMP 字符网格上。

```csharp
mTextAnimator.Play("获得 <fx=highlight>普通</fx> 物品");
```

这句代码最终会经历：保存原文 → 解析标签 → 得到字符范围 → 每帧计算状态 → 修改 TMP 顶点。

> **两种用法**：① 文案固定 → 直接在 TMP 上写标签 + 勾选 `UITextAnimator` 的 **Play On Enable**，零代码；② 文案运行时才确定 → 调用 `Play(文本)`。详见“五、`UITextAnimator.cs` → Unity 生命周期方法”。

---

## 一、`UITextAnimationEffect.cs`

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextAnimationEffect.cs`

这个脚本先定义“一个字符当前应该怎样显示”的数据，然后定义“效果怎样修改这份数据”的接口，最后实现五种内置效果。

### 1. `UITextCharacterState`

源码：

```csharp
public struct UITextCharacterState
{
    public bool Visible;
    public float Alpha;
    public float Scale;
    public Vector3 Offset;
    public Color32 Color;
}
```

解释：

- `Visible`：字符是否显示。
- `Alpha`：透明度倍率，初始值通常是 `1`。
- `Scale`：缩放倍率，初始值通常是 `1`。
- `Offset`：字符顶点的位移。
- `Color`：字符颜色。

它是一次计算中的临时数据，不是组件，也不是资源。`UITextAnimator` 为每个字符创建一份状态，多个 Effect 依次修改同一份状态，最后 Animator 再把最终状态写回 TMP。

### 2. `UITextAnimationContext`

源码：

```csharp
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
```

解释：

`Effect.Apply` 需要知道当前处理哪个字符、范围从哪里开始、动画已经播放多久。Animator 不把自己整个传进去，而是创建这个小型上下文，只提供计算需要的数据。

`readonly` 表示 Effect 可以读取这些数据，但不能修改它们。这样 Effect 不会偷偷改变 Animator 的播放时间或字符位置。

继续看同一个结构体中的属性：

```csharp
    public float RangeProgress
    {
        get
        {
            var length = Mathf.Max(1, RangeEnd - RangeStart);
            return Mathf.Clamp01(
                (CharacterIndex - RangeStart) / (float)length);
        }
    }
}
```

逐句看：

```csharp
var length = Mathf.Max(1, RangeEnd - RangeStart);
```

`RangeEnd - RangeStart` 是范围长度。用 `Mathf.Max(1, ...)` 是为了防止长度为零时除零。

```csharp
(CharacterIndex - RangeStart) / (float)length
```

当前字符距离范围起点越近，结果越接近 `0`；越接近范围末尾，结果越接近 `1`。转换为 `float` 是为了得到小数，而不是整数除法。

```csharp
Mathf.Clamp01(...)
```

把结果限制到 `0` 到 `1`。`Clamp01` 是固定范围版本；普通 `Clamp` 可以传入任意最小值和最大值。

### 3. Effect 基类

源码：

```csharp
public abstract class UITextAnimationEffect : ScriptableObject
{
    [SerializeField, Min(0.01f)] private float mDuration = 0.2f;
    [SerializeField] private AnimationCurve mCurve = null;

    protected float Duration => mDuration;
```

解释：

`abstract` 表示它只是所有文字效果的共同基类，不能直接创建；具体效果需要继承它并实现 `Apply`。

`ScriptableObject` 让效果可以保存为 Unity Project 面板中的 `.asset` 文件。持续时间、曲线、振幅等配置可以被多个 `UITextAnimator` 复用。它在这里不是管理器，也不负责播放；播放状态仍然属于 `UITextAnimator`。

`mDuration` 是效果配置，`mCurve` 是可选的速度曲线。`Duration => mDuration` 是只读访问器，子类可以读取时长，外部不能直接修改。

继续看基类方法：

```csharp
    protected float Evaluate(float elapsed)
    {
        var progress = Mathf.Clamp01(
            elapsed / Mathf.Max(0.01f, mDuration));
        return mCurve == null
            ? progress
            : mCurve.Evaluate(progress);
    }

    public abstract void Apply(
        ref UITextCharacterState state,
        UITextAnimationContext context);
}
```

`Evaluate` 先把经过的秒数换算成 `0` 到 `1` 的进度，再使用曲线改变速度。曲线改变的是节奏，不是总时长。

`Apply` 是所有具体效果必须实现的方法。`state` 使用 `ref`，因为 `UITextCharacterState` 是结构体；没有 `ref` 时传入的是副本，效果修改不会传回 Animator。使用 `ref` 后，Fade 修改过的 Alpha 可以继续交给 Scale 或 Shake 使用。

### 4. Typewriter 效果

源码：

```csharp
[CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Typewriter")]
public sealed class UITextTypewriterEffect : UITextAnimationEffect
{
    [SerializeField, Min(0f)] private float mCharacterDelay = 0.035f;

    public override void Apply(
        ref UITextCharacterState state,
        UITextAnimationContext context)
    {
        state.Visible = context.Elapsed >=
            (context.CharacterIndex - context.RangeStart)
            * mCharacterDelay;
        if (!state.Visible)
        {
            state.Alpha = 0f;
        }
    }
}
```

`CreateAssetMenu` 让这个类型可以在 Project 面板创建资源。`mCharacterDelay` 表示相邻字符的出现间隔。

当前字符在范围中的序号是：

```csharp
context.CharacterIndex - context.RangeStart
```

序号乘以间隔，就是它应该出现的时间。未到时间时把 `Visible` 设为 `false`，并把 Alpha 设为 `0`。

### 5. Fade、Float、Shake、Scale

源码：

```csharp
[CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Fade In")]
public sealed class UITextFadeInEffect : UITextAnimationEffect
{
    [SerializeField, Min(0f)] private float mCharacterDelay = 0.02f;

    public override void Apply(
        ref UITextCharacterState state,
        UITextAnimationContext context)
    {
        state.Alpha *= Evaluate(
            context.Elapsed -
            (context.CharacterIndex - context.RangeStart)
            * mCharacterDelay);
    }
}
```

Fade 为每个字符减去自己的延迟，再把结果转换成透明度进度。使用 `*=` 是为了和已有 Alpha 叠加。

```csharp
[CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Float")]
public sealed class UITextFloatEffect : UITextAnimationEffect
{
    [SerializeField] private float mAmplitude = 2f;
    [SerializeField, Min(0.01f)] private float mFrequency = 4f;
    [SerializeField, Min(0f)] private float mCharacterOffset = 0.08f;

    public override void Apply(
        ref UITextCharacterState state,
        UITextAnimationContext context)
    {
        var phase = (context.Elapsed +
            context.CharacterIndex * mCharacterOffset)
            * mFrequency;
        state.Offset.y += Mathf.Sin(phase) * mAmplitude;
    }
}
```

`Mathf.Sin` 产生周期性上下运动；字符索引参与相位，所以每个字符不会完全同步。

```csharp
[CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Shake")]
public sealed class UITextShakeEffect : UITextAnimationEffect
{
    [SerializeField] private float mAmplitude = 1.5f;
    [SerializeField, Min(0.01f)] private float mFrequency = 18f;

    public override void Apply(
        ref UITextCharacterState state,
        UITextAnimationContext context)
    {
        var time = context.Elapsed * mFrequency
            + context.CharacterIndex * 1.37f;
        state.Offset += new Vector3(
            Mathf.Sin(time),
            Mathf.Cos(time * 1.17f),
            0f) * mAmplitude;
    }
}
```

X、Y 使用不同的正弦变化，产生抖动；字符索引产生不同相位。

```csharp
[CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Scale In")]
public sealed class UITextScaleInEffect : UITextAnimationEffect
{
    [SerializeField, Range(0f, 1f)] private float mStartScale = 0.7f;
    [SerializeField, Min(0f)] private float mCharacterDelay = 0.02f;

    public override void Apply(
        ref UITextCharacterState state,
        UITextAnimationContext context)
    {
        var progress = Evaluate(
            context.Elapsed -
            (context.CharacterIndex - context.RangeStart)
            * mCharacterDelay);
        state.Scale *= Mathf.Lerp(mStartScale, 1f, progress);
    }
}
```

`Lerp` 在起始缩放和正常缩放之间插值。`Range(0, 1)` 只限制 Inspector 输入范围，不是动画逻辑。

---

## 二、`UITextEffectBinding.cs`

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextEffectBinding.cs`

源码：

```csharp
[Serializable]
public sealed class UITextEffectBinding
{
    [Tooltip("文本标签使用的 ID，例如 shake、fade、scale 或 highlight")]
    public string Id;

    [Tooltip("该 ID 对应的效果资源列表，执行顺序就是列表顺序")]
    public List<UITextAnimationEffect> Effects =
        new List<UITextAnimationEffect>();
}
```

这个类没有播放方法，它只是让 Unity 能序列化一组 Inspector 配置。

例如：

```text
Id      = highlight
Effects = FadeIn.asset, ScaleIn.asset, Shake.asset
```

文本写 `<fx=highlight>` 时，运行时会把它转换为这三个效果。`Effects` 的排列顺序就是执行顺序。

---

## 三、`UITextEffectRange.cs`

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextEffectRange.cs`

源码：

```csharp
public sealed class UITextEffectRange
{
    public int Start;
    public int EndExclusive;
    public readonly List<UITextAnimationEffect> Effects =
        new List<UITextAnimationEffect>();

    public bool Contains(int characterIndex)
    {
        return characterIndex >= Start
            && characterIndex < EndExclusive;
    }
}
```

它表示解析完成后的一段实际字符范围。`Start` 包含，`EndExclusive` 不包含。例如 5 到 7 实际包含索引 5、6。

`Contains` 把边界判断集中在 Range 类中，Animator 不需要重复写相同条件。Range 不负责解析、播放或修改顶点。

---

## 四、`UITextMarkupParser.cs`

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextMarkupParser.cs`

### 1. 返回值类型

源码：

```csharp
public sealed class UITextMarkupResult
{
    public string Text;
    public readonly List<UITextEffectRange> Ranges =
        new List<UITextEffectRange>();
}
```

解析器必须同时返回两样东西：去掉框架标签后的文本，以及标签对应的字符范围。

### 2. `Parse` 完整源码

```csharp
public static UITextMarkupResult Parse(string source)
{
    var result = new UITextMarkupResult();
    var output = new StringBuilder();
    var openRanges = new Stack<UITextEffectRange>();
    var characterCount = 0;

    for (var i = 0; i < source.Length; i++)
    {
        if (source[i] != '<')
        {
            output.Append(source[i]);
            characterCount++;
            continue;
        }

        var close = source.IndexOf('>', i);
        if (close < 0)
        {
            output.Append(source[i]);
            characterCount++;
            continue;
        }

        var tag = source.Substring(i + 1,
            close - i - 1).Trim();
        if (tag == "/fx")
        {
            if (openRanges.Count > 0)
            {
                var range = openRanges.Pop();
                range.EndExclusive = characterCount;
                result.Ranges.Add(range);
            }

            i = close;
            continue;
        }

        if (tag.StartsWith("fx="))
        {
            var range = new UITextEffectRange();
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
                            range.Effects.Add(effect);
                        }
                    }
                }
            }

            range.Start = characterCount;
            openRanges.Push(range);
            i = close;
            continue;
        }

        // 不是框架标签，例如 <color> 或 <size>，交回 TMP。
        output.Append(source, i, close - i + 1);
        i = close;
    }

    result.Text = output.ToString();
    return result;
}
```

### 3. 对照源码理解执行过程

```csharp
var output = new StringBuilder();
var openRanges = new Stack<UITextEffectRange>();
var characterCount = 0;
```

`StringBuilder` 用来构造清理后的文本。`Stack` 保存已经遇到开始标签、还没有遇到结束标签的范围。`characterCount` 统计输出文本的位置，不是字节数。

```csharp
if (source[i] != '<')
{
    output.Append(source[i]);
    characterCount++;
    continue;
}
```

普通字符直接保留，并计入输出位置。

```csharp
var close = source.IndexOf('>', i);
if (close < 0)
{
    output.Append(source[i]);
    characterCount++;
    continue;
}
```

找不到 `>` 时，把 `<` 当普通字符处理，避免不完整标签导致解析流程失控。

```csharp
var tag = source.Substring(i + 1,
    close - i - 1).Trim();
```

截取尖括号内部的文字。例如 `<fx=shake>` 会得到 `fx=shake`。

```csharp
if (tag == "/fx")
{
    var range = openRanges.Pop();
    range.EndExclusive = characterCount;
    result.Ranges.Add(range);
    i = close;
    continue;
}
```

遇到结束标签时，取出最近开始的范围，把当前输出位置记为结束位置。标签本身不写入输出文本。

```csharp
var range = new UITextEffectRange();
var ids = tag.Substring(3).Split(',');
```

遇到开始标签时，去掉 `fx=`，再按逗号拆出多个 ID。

```csharp
var effects = UITextEffectLibrary.FindEffects(id);
if (effects != null)
{
    foreach (var effect in effects)
    {
        if (effect != null)
        {
            range.Effects.Add(effect);
        }
    }
}
```

通过项目级效果库按 ID 找到 Effect 列表，并加入当前 Range。找不到的 ID 会被忽略。

```csharp
range.Start = characterCount;
openRanges.Push(range);
```

记录开始位置，把范围压入栈，等待后续 `</fx>`。

```csharp
output.Append(source, i, close - i + 1);
```

既不是 `/fx` 也不是 `fx=` 的标签原样保留，例如 `<color>` 和 `<size>` 继续交给 TMP。

---

## 五、`UITextAnimator.cs`

文件：`Assets/Framework/Runtime/UI/Components/TextEffect/UITextAnimator.cs`

### 1. 类声明和字段

源码：

```csharp
[RequireComponent(typeof(TMP_Text))]
public sealed class UITextAnimator : MonoBehaviour
{
    [SerializeField] private TMP_Text mText;
    [SerializeField, Min(0.01f)] private float mDuration = 0.5f;
    [SerializeField] private bool mPlayOnEnable;
    [SerializeField] private List<UITextAnimationEffect> mGlobalEffects =
        new List<UITextAnimationEffect>();

    private TMP_MeshInfo[] mBaseMeshInfo;
    private List<UITextEffectRange> mRanges =
        new List<UITextEffectRange>();
    private string mSourceText;
    private bool mHasSourceText;
    private float mElapsed;
    private bool mPlaying;
    private bool mPaused;
```

`RequireComponent` 保证同一 GameObject 上存在 TMP 文本。

Inspector 字段：

- `mText`：目标 TMP 文本。
- `mDuration`：Animator 这一轮播放的总时长。
- `mPlayOnEnable`：启用时是否自动播放。
- `mGlobalEffects`：所有可见字符都执行的效果。
- 项目级 ID 到 Effect 的配置由 `UITextEffectLibrary` 通过 Resources 统一提供，Animator 不再保存绑定表或库引用。

运行时字段：

- `mBaseMeshInfo`：播放开始时的原始网格。
- `mRanges`：本次文本解析得到的局部范围。
- `mSourceText`：保留带 `<fx>` 标签的原始文本。
- `mHasSourceText`：是否已经保存原文。
- `mElapsed`：已经播放的时间。
- `mPlaying`、`mPaused`：播放状态。

### 2. 状态属性和事件

源码：

```csharp
public bool IsPlaying => mPlaying;
public bool IsPaused => mPaused;
public event Action Completed;
```

两个属性只允许外部查询，不允许外部直接修改内部播放字段。`Completed` 在动画正常结束或立即完成时通知调用者。

### 3. Unity 生命周期方法

源码：

```csharp
private void Reset()
{
    mText = GetComponent<TMP_Text>();
}

private void Awake()
{
    if (mText == null)
    {
        mText = GetComponent<TMP_Text>();
    }
}

private void OnEnable()
{
    if (mPlayOnEnable)
    {
        Play();
    }
}

private void OnDisable()
{
    Stop(true);
}
```

`Reset` 在组件添加或 Inspector 执行 Reset 时帮助填入 TMP 引用。`Awake` 是运行时兜底，防止 Prefab 没有保存引用。

`OnEnable` 按配置自动播放。`OnDisable` 停止并恢复原始网格，适配页面关闭、Prefab 禁用和对象池复用。

> **无代码用法（推荐用于固定文案）**：把标签直接写进 TMP 文本，例如 `<fx=idle_shake loop>警告：网络不稳定</fx>`，给同一物体挂上 `UITextAnimator` 并勾选 **Play On Enable**，启用时会自动解析并播放（**ID 以 `UITextEffectLibrary.asset` 里注册的为准，未注册的 ID 会被静默忽略**，可用 ID 见库资产或演示页分组）。`Play()` 会缓存原始文本（`mSourceText`），因此页面重复打开、对象池复用都会按原标签重新播放；只有运行时换文案才需要调用 `Play(文本)`。

### 4. `Play()` 和 `Play(string)`

源码：

```csharp
public void Play()
{
    var text = mHasSourceText
        ? mSourceText
        : mText == null ? string.Empty : mText.text;
    Play(text);
}
```

这个方法只负责选择文本，然后统一交给带参数版本。优先使用 `mSourceText`，是因为解析器会移除 `<fx>` 标签；如果只读取当前 `mText.text`，重播时局部效果就会消失。

完整的带参数版本：

```csharp
public void Play(string text)
{
    if (mText == null)
    {
        return;
    }

    mSourceText = text ?? string.Empty;
    mHasSourceText = true;
    var parsed = UITextMarkupParser.Parse(mSourceText);
    mRanges = parsed.Ranges;
    mText.text = parsed.Text;
    mText.ForceMeshUpdate();
    mBaseMeshInfo = mText.textInfo.CopyMeshInfoVertexData();
    mElapsed = 0f;
    mPlaying = true;
    mPaused = false;
    ApplyFrame();
}
```

按执行顺序：

```csharp
if (mText == null) return;
```

没有目标文本就没有可以修改的网格，因此提前结束。

```csharp
mSourceText = text ?? string.Empty;
mHasSourceText = true;
```

保存带标签的原文，并把 `null` 转为空字符串。

```csharp
var parsed = UITextMarkupParser.Parse(mSourceText);
mRanges = parsed.Ranges;
```

解析器从项目级 `UITextEffectLibrary` 查找 ID；解析结果中的范围保存到本轮播放。

```csharp
mText.text = parsed.Text;
mText.ForceMeshUpdate();
```

把去掉框架标签的文本交给 TMP，并要求 TMP 立即重新生成字符信息。

```csharp
mBaseMeshInfo = mText.textInfo.CopyMeshInfoVertexData();
```

复制一份没有被动画修改的网格。每帧都以它为基准重新计算，避免上一帧缩放和位移累积。

```csharp
mElapsed = 0f;
mPlaying = true;
mPaused = false;
ApplyFrame();
```

初始化播放状态，并立即计算第一帧。

### 5. 播放控制方法

源码：

```csharp
public void Pause()
{
    if (mPlaying)
    {
        mPaused = true;
    }
}

public void Resume()
{
    if (mPlaying)
    {
        mPaused = false;
    }
}
```

暂停只停止时间推进，保留当前网格；继续播放只解除暂停标记。

```csharp
public void CompleteImmediately()
{
    if (mBaseMeshInfo == null)
    {
        return;
    }

    mElapsed = mDuration;
    ApplyFrame();
    mPlaying = false;
    mPaused = false;
    Completed?.Invoke();
}
```

把时间直接设置到结束位置，计算最终帧，再结束播放并触发事件。

```csharp
public void Stop(bool reset)
{
    mPlaying = false;
    mPaused = false;
    if (reset && mText != null && mBaseMeshInfo != null)
    {
        RestoreBaseMesh();
    }
}
```

停止始终清除播放状态；`reset` 为 `true` 时额外恢复播放开始时的网格。`OnDisable` 使用 `Stop(true)`，因此对象池重新启用时不会残留变形。

### 6. `Update`

源码：

```csharp
private void Update()
{
    if (!mPlaying || mPaused)
    {
        return;
    }

    mElapsed += Time.unscaledDeltaTime;
    ApplyFrame();
    if (mElapsed < mDuration)
    {
        return;
    }

    mPlaying = false;
    Completed?.Invoke();
}
```

没有播放或处于暂停时直接返回。`unscaledDeltaTime` 让 UI 动画不受 `Time.timeScale` 影响。每帧推进时间并调用 `ApplyFrame`；时间达到总时长时结束并通知监听者。

### 7. `ApplyFrame`：逐字符计算状态

源码第一段：

```csharp
private void ApplyFrame()
{
    var textInfo = mText.textInfo;
    for (var characterIndex = 0;
         characterIndex < textInfo.characterCount;
         characterIndex++)
    {
        var character = textInfo.characterInfo[characterIndex];
        if (!character.isVisible)
        {
            continue;
        }

        var materialIndex = character.materialReferenceIndex;
        var vertexIndex = character.vertexIndex;
        var baseVertices =
            mBaseMeshInfo[materialIndex].vertices;
        var vertices =
            textInfo.meshInfo[materialIndex].vertices;
```

`characterInfo` 保存每个字符的材质索引和顶点索引。空格、换行等字符没有可见四边形，所以跳过。

`baseVertices` 是原始顶点；`vertices` 是本帧要写回的顶点。TMP 可能有多个材质网格，所以必须先使用 `materialReferenceIndex` 找到正确网格。

源码第二段：

```csharp
        var state = new UITextCharacterState
        {
            Visible = true,
            Alpha = 1f,
            Scale = 1f,
            Offset = Vector3.zero,
            Color = mBaseMeshInfo[materialIndex]
                .colors32[vertexIndex]
        };

        var range = FindRange(characterIndex);
        var rangeStart = range == null ? 0 : range.Start;
        var rangeEnd = range == null
            ? textInfo.characterCount
            : range.EndExclusive;
        var context = new UITextAnimationContext(
            characterIndex,
            rangeStart,
            rangeEnd,
            mElapsed,
            mDuration);

        ApplyEffects(mGlobalEffects, ref state, context);
        if (range != null)
        {
            ApplyEffects(range.Effects, ref state, context);
        }
```

每个字符先从正常状态开始，再查找它是否属于局部范围。上下文把字符索引、范围和时间传给 Effect。

全局效果先执行，局部效果后执行。由于 `state` 使用 `ref`，多个效果修改的是同一个状态。

源码第三段：

```csharp
        var center = (baseVertices[vertexIndex]
            + baseVertices[vertexIndex + 2]) * 0.5f;
        var matrix = Matrix4x4.TRS(
            center + state.Offset,
            Quaternion.identity,
            Vector3.one * state.Scale);

        for (var vertex = 0; vertex < 4; vertex++)
        {
            vertices[vertexIndex + vertex] =
                matrix.MultiplyPoint3x4(
                    baseVertices[vertexIndex + vertex]);

            var color = state.Color;
            color.a = state.Visible
                ? (byte)Mathf.Clamp(
                    Mathf.RoundToInt(color.a * state.Alpha),
                    0, 255)
                : (byte)0;
            textInfo.meshInfo[materialIndex].colors32[
                vertexIndex + vertex] = color;
        }
    }

    mText.UpdateVertexData(
        TMP_VertexDataUpdateFlags.Vertices |
        TMP_VertexDataUpdateFlags.Colors32);
}
```

一个字符由四个顶点组成，四个顶点必须使用同一个矩阵，否则字符会被拉扯。矩阵以字符中心为基准应用位移和缩放。

透明度最终被限制到 `0` 到 `255`，因为 `Color32.a` 是字节。最后调用 `UpdateVertexData`，把修改后的顶点和颜色提交给 TMP。

### 9. `ApplyEffects`

源码：

```csharp
private void ApplyEffects(
    List<UITextAnimationEffect> effects,
    ref UITextCharacterState state,
    UITextAnimationContext context)
{
    if (effects == null)
    {
        return;
    }

    foreach (var effect in effects)
    {
        if (effect != null)
        {
            effect.Apply(ref state, context);
        }
    }
}
```

这个方法只负责遍历和调用，不知道 Fade、Shake 或 Scale 的具体算法。它保证列表顺序就是效果叠加顺序。

### 10. `FindRange`

源码：

```csharp
private UITextEffectRange FindRange(int characterIndex)
{
    foreach (var range in mRanges)
    {
        if (range.Contains(characterIndex))
        {
            return range;
        }
    }

    return null;
}
```

对当前范围列表逐个检查，返回第一个包含当前字符的范围。当前实现优先保证简单易懂；如果以后范围数量很大，可以把字符索引预先映射到 Range。

### 11. `RestoreBaseMesh`

源码：

```csharp
private void RestoreBaseMesh()
{
    var textInfo = mText.textInfo;
    for (var i = 0; i < textInfo.meshInfo.Length; i++)
    {
        Array.Copy(
            mBaseMeshInfo[i].vertices,
            textInfo.meshInfo[i].vertices,
            mBaseMeshInfo[i].vertices.Length);
        Array.Copy(
            mBaseMeshInfo[i].colors32,
            textInfo.meshInfo[i].colors32,
            mBaseMeshInfo[i].colors32.Length);
    }

    mText.UpdateVertexData(
        TMP_VertexDataUpdateFlags.Vertices |
        TMP_VertexDataUpdateFlags.Colors32);
}
```

它把每个材质网格的顶点和颜色复制回播放开始时的备份，再通知 TMP 刷新。它不负责重新解析文本，也不负责修改播放标记；这些职责分别属于 `Play` 和 `Stop`。

---

## 六、把五个脚本连起来

现在回到最初的调用：

```csharp
mTextAnimator.Play("获得 <fx=highlight>普通</fx> 物品");
```

对应关系如下：

```text
UITextAnimator.Play(string)
    调用 UITextMarkupParser.Parse
    保存 UITextEffectRange
    保存 TMP 原始网格

UITextMarkupParser
    把 highlight 查成 Binding.Effects
    记录“普通”字符的 Start 和 EndExclusive

UITextAnimator.Update
    调用 ApplyFrame

ApplyFrame
    创建 UITextCharacterState
    创建 UITextAnimationContext
    调用每个 UITextAnimationEffect.Apply
    修改字符四个顶点和颜色

UITextAnimator
    调用 TMP_Text.UpdateVertexData
```

因此四个核心对象分别承担：

```text
Library = 全局唯一的 ID 配置
Parser  = 字符串转结构并查询 Library
Range   = 保存局部字符范围
Effect  = 修改字符状态
Animator = 管理播放并写回 TMP
```

## 七、播放模式：效果资源与播放策略分离

新增的 `UITextEffectPlayback` 只描述当前范围如何播放：

```csharp
public enum Mode
{
    Once,
    Loop
}
```

解析器支持：

```text
<fx=highlight>文字</fx>
<fx=highlight once>文字</fx>
<fx=highlight loop>文字</fx>
```

三种写法都使用同一个全局 `highlight` 效果配置。`once` 和 `loop` 是本次范围的播放修饰词，不需要创建 `highlight_once`、`highlight_loop` 两套效果绑定。

当前 `UITextEffectLibrary.asset` 配置了以下组合：

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

这些组合不是代码硬编码的默认值，运行时只从 `UITextEffectLibrary.asset` 查找。以后新增组合时，只需在该资产中添加新的 `Id` 和 `Effects` 列表。

`UITextEffectPlayback.GetElapsed` 的作用是把 Animator 的总时间转换成范围时间：

```csharp
return IsLooping
    ? Mathf.Repeat(elapsed, safeDuration)
    : Mathf.Min(elapsed, safeDuration);
```

循环范围到达持续时间后从零开始；一次性范围到达结束后保持最终时间。

Animator 只有在存在循环范围时才会继续运行；一次性范围会停在最终状态。这样同一段文字中可以同时存在一次性效果和循环效果。

## 八、总结和当前边界

当前实现支持：

- 全局效果。
- 一个范围中组合多个效果。
- TMP 标签与框架 `fx` 标签共存。
- 播放、暂停、继续、立即完成、停止和完成事件。
- ScriptableObject 效果资源和 AnimationCurve。

当前没有实现：

- 标签内直接写 `shake(amplitude=2)` 的参数。
- Cycle 模式的淡入、保持、淡出和指定循环次数。
- 复杂重叠范围的优先级合并。
- 大量范围的字符索引缓存。
- Unity 编辑器中的实际视觉回归测试。

这份文档的阅读方法也对应代码的设计方法：先看调用者传入什么，再看当前类修改了什么状态，最后看结果交给谁。新增 Effect 时，通常只需要继承基类并实现 `Apply`，不用把动画算法复制到每个 UI 页面。
