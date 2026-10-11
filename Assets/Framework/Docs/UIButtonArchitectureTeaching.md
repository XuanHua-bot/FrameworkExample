# UI 组件库实作 01：符合四层架构的统一 UIButton

本篇是新的教学文档，不修改以下旧文档：

```text
UIButtonTeaching.md
UIButtonTeachingV2.md
UIButtonNetworkTeaching.md
```

本篇只实现按钮组件的职责和接入方式，不创建网络层、商城系统或错误提示系统。那些模块需要在后续按照当前框架的四层规则分别实现。

## 一、最终目标

制作 UI 时只使用一个按钮组件：

```text
UIButton
├── 接收用户点击
├── 播放点击音效
├── 发送 Clicked 事件
├── 进入 Busy 状态
├── 结束 Busy 状态
├── 设置 Disabled 状态
└── 可选 Cooldown 状态
```

普通按钮和网络操作按钮都使用 `UIButton`。区别只在于页面 Controller 是否在点击后发送 Command，以及是否在等待结果期间调用 Busy。

## 二、当前框架中的层级位置

```text
UIButton（MonoBehaviour）
    表现层控件

页面 ViewController
    接收 UIButton.Clicked
    查询 Model 或 System
    发送 Command
    监听 Event

Command
    描述一次业务操作

System
    执行业务流程
    调用 Model 和 Utility
    发送结果 Event

Model
    保存业务数据
    提供数据查询和修改方法

Utility
    提供网络、资源、音频等基础设施
```

### 按钮不能做的事情

`UIButton` 不应该直接：

```text
修改背包数据
扣除玩家货币
发送网络协议
解析服务器错误码
创建错误弹窗
```

这些职责属于业务 System、Model、网络 Utility 和统一消息服务。

## 三、文件一：按钮状态

文件：`Assets/Framework/Runtime/UI/Components/UIButtonState.cs`

```csharp
namespace GameFramework
{
    /// <summary>
    /// 描述统一 UIButton 当前所处的交互状态。
    /// </summary>
    public enum UIButtonState
    {
        /// <summary>
        /// 按钮可以接收用户点击。
        /// </summary>
        Normal,

        /// <summary>
        /// 按钮正在等待一次业务操作完成。
        /// Busy 状态会阻止重复点击。
        /// </summary>
        Busy,

        /// <summary>
        /// 按钮因为业务条件不满足而不可用。
        /// </summary>
        Disabled,

        /// <summary>
        /// 按钮处于业务规定的冷却时间内。
        /// </summary>
        Cooldown
    }
}
```

## 四、文件二：统一 UIButton

文件：`Assets/Framework/Runtime/UI/Components/UIButton.cs`

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 项目统一按钮组件。
    ///
    /// 它建立在 Unity 原生 Button 上，只增加项目通用的交互状态、
    /// 点击事件和音效请求，不包含具体游戏业务。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButton : MonoBehaviour
    {
        /// <summary>
        /// Unity 原生 Button。
        /// 它负责射线检测、键盘导航和基础 interactable 行为。
        /// </summary>
        private Button mButton;

        /// <summary>
        /// 进入 Busy 前按钮是否可交互。
        /// 请求结束时按这个值恢复，而不是无条件恢复为 true。
        /// </summary>
        private bool mInteractableBeforeBusy;

        /// <summary>
        /// 是否正在等待业务操作完成。
        /// </summary>
        private bool mIsBusy;

        /// <summary>
        /// 当前按钮的点击音效 Key。
        /// Group 和 Cue 留空表示此按钮没有点击音效。
        /// </summary>
        [SerializeField]
        private AudioCueKey mClickSound;

        /// <summary>
        /// 当前按钮是否可交互。
        /// </summary>
        public bool Interactable => mButton != null && mButton.interactable;

        /// <summary>
        /// 当前按钮是否处于 Busy 状态。
        /// </summary>
        public bool IsBusy => mIsBusy;

        /// <summary>
        /// 当前按钮的状态。
        /// </summary>
        public UIButtonState State { get; private set; }

        /// <summary>
        /// 按钮完成一次有效点击后触发。
        /// 页面 Controller 应订阅这个事件并执行查询或发送 Command。
        /// </summary>
        public event Action Clicked;

        /// <summary>
        /// 按钮需要播放点击音效时触发。
        /// 音频服务由外部订阅，按钮本身不直接依赖 AudioService。
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        /// <summary>
        /// Unity 生命周期初始化方法。
        /// 获取同一个 GameObject 上的原生 Button，并设置初始状态。
        /// </summary>
        private void Awake()
        {
            mButton = GetComponent<Button>();
            State = mButton.interactable
                ? UIButtonState.Normal
                : UIButtonState.Disabled;
        }

        /// <summary>
        /// 组件启用时注册原生 Button 的点击监听。
        /// </summary>
        private void OnEnable()
        {
            mButton.onClick.AddListener(HandleUnityClick);
        }

        /// <summary>
        /// 组件禁用时移除点击监听，避免重复注册。
        /// </summary>
        private void OnDisable()
        {
            mButton.onClick.RemoveListener(HandleUnityClick);
        }

        /// <summary>
        /// 设置按钮的业务可用状态。
        /// Busy 期间不允许外部覆盖按钮状态。
        /// </summary>
        /// <param name="value">true 表示可交互，false 表示禁用。</param>
        public void SetInteractable(bool value)
        {
            if (mIsBusy) return;

            mButton.interactable = value;
            State = value ? UIButtonState.Normal : UIButtonState.Disabled;
        }

        /// <summary>
        /// 尝试进入 Busy 状态。
        /// 网络操作开始前调用，成功后按钮会立即禁止重复点击。
        /// </summary>
        /// <returns>
        /// 成功进入 Busy 返回 true；按钮当前不可用或已经 Busy 时返回 false。
        /// </returns>
        public bool TryBeginBusy()
        {
            if (mIsBusy || !mButton.interactable ||
                State == UIButtonState.Cooldown)
            {
                return false;
            }

            mInteractableBeforeBusy = mButton.interactable;
            mIsBusy = true;
            State = UIButtonState.Busy;
            mButton.interactable = false;
            return true;
        }

        /// <summary>
        /// 结束 Busy 状态。
        /// 请求成功、失败、超时和取消时都必须调用。
        /// </summary>
        public void EndBusy()
        {
            if (!mIsBusy) return;

            mIsBusy = false;
            mButton.interactable = mInteractableBeforeBusy;
            State = mButton.interactable
                ? UIButtonState.Normal
                : UIButtonState.Disabled;
        }

        /// <summary>
        /// 进入冷却状态。
        /// 冷却计时由业务层或专用冷却组件负责。
        /// </summary>
        public void BeginCooldown()
        {
            if (State != UIButtonState.Normal) return;

            mButton.interactable = false;
            State = UIButtonState.Cooldown;
        }

        /// <summary>
        /// 结束冷却并恢复按钮。
        /// </summary>
        public void ClearCooldown()
        {
            if (State != UIButtonState.Cooldown) return;

            mButton.interactable = true;
            State = UIButtonState.Normal;
        }

        /// <summary>
        /// 接收 Unity 原生 Button 的点击回调。
        /// 只有 Normal 状态的点击才会向 Controller 广播。
        /// </summary>
        private void HandleUnityClick()
        {
            if (State != UIButtonState.Normal || !mButton.interactable)
            {
                return;
            }

            if (mClickSound.IsValid)
            {
                SoundRequested?.Invoke(mClickSound);
            }

            Clicked?.Invoke();
        }
    }
}
```

## 五、音效接入

当前项目的 `AudioCueKey` 尚未标记 `[Serializable]`，也没有 `IsValid` 属性。为了让 Inspector 能配置 Key，并让空 Key 表示“无音效”，把 `Assets/Framework/Runtime/Audio/AudioCueKey.cs` 写成：

```csharp
using System;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 音频事件的地址：资源组 Key 加组内 Cue ID。
    /// </summary>
    [Serializable]
    public struct AudioCueKey
    {
        [SerializeField] private string mGroup;
        [SerializeField] private string mCue;

        /// <summary>音频资源组的加载 Key。</summary>
        public string Group => mGroup;

        /// <summary>组内的音频事件 ID。</summary>
        public string Cue => mCue;

        /// <summary>两个字段均已配置时才可以用于播放。</summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(mGroup) &&
            !string.IsNullOrWhiteSpace(mCue);

        /// <summary>创建一个音频事件地址，供代码调用。</summary>
        public AudioCueKey(string group, string cue)
        {
            mGroup = group;
            mCue = cue;
        }
    }
}
```

`[Serializable]` 让 Unity 在 Inspector 中显示并保存 `mGroup`、`mCue`。`IsValid` 只检查地址是否完整，不代表资源组已经加载、Cue 一定存在；这些仍由 `AudioService` 在播放时检查。

按钮通过 `AudioCueKey` 保存音频标识，不在点击时重复 `new AudioCueKey`。`Group` 或 `Cue` 留空时不发出音效请求；要播放音效，则配置完整 Key，保证音频组在点击前已经加载，并由页面或统一绑定组件订阅 `SoundRequested`。

页面 Controller 可以订阅音效事件：

```csharp
/// <summary>
/// 页面打开时注册音效监听。
/// </summary>
public override void OnOpen()
{
    mBuyButton.SoundRequested += OnSoundRequested;
}

/// <summary>
/// 页面关闭时解除音效监听。
/// </summary>
public override void OnClose()
{
    mBuyButton.SoundRequested -= OnSoundRequested;
}

/// <summary>
/// 把按钮的音效请求交给音频 Utility。
/// </summary>
private void OnSoundRequested(AudioCueKey key)
{
    ExampleArchitecture.Interface
        .GetUtility<IAudioService>()
        .Play(key);
}
```

上面是单页面接线示例。如果许多页面都要播放同一种点击音效，后续应由统一 UI 层的绑定组件处理，避免每个页面都重复写订阅代码。本篇不先引入新的音效服务。

## 六、普通按钮的 Controller 用法

普通关闭、返回和 Tab 按钮不需要 Busy：

```csharp
using GameFramework;
using UnityEngine;

namespace GameFramework.Examples
{
    /// <summary>
    /// 示例页面 Controller。
    /// 负责接收表现层输入，并调用 UI 基础服务。
    /// </summary>
    public sealed class ExampleButtonPage : UIPage, IController
    {
        /// <summary>
        /// 页面关闭按钮。
        /// </summary>
        [SerializeField]
        private UIButton mCloseButton;

        /// <summary>
        /// 页面打开时注册表现层事件。
        /// </summary>
        public override void OnOpen()
        {
            mCloseButton.Clicked += OnCloseClicked;
        }

        /// <summary>
        /// 页面关闭时解除表现层事件。
        /// </summary>
        public override void OnClose()
        {
            mCloseButton.Clicked -= OnCloseClicked;
        }

        /// <summary>
        /// 响应关闭按钮。
        /// 这里只调用 UI 基础设施，不修改游戏业务状态。
        /// </summary>
        private void OnCloseClicked()
        {
            this.GetUtility<IUGUIService>().Close(this);
        }

        /// <summary>
        /// 把本页面绑定到示例 Architecture。
        /// </summary>
        IArchitecture IBelongArchitecture.GetArchitecture()
        {
            return ExampleArchitecture.Interface;
        }
    }
}
```

这里直接调用 `IUGUIService` 是合理的，因为关闭页面属于 UI 基础设施操作，不是商城、背包或角色数据的业务状态修改。

## 七、网络业务按钮的正确边界

本篇**不创建** `BuyItemCommand`、`IPurchaseSystem`、网络 Utility 或购买结果事件。当前项目还没有这些类型，把它们写成可手抄的 C# 代码会造成编译错误。

等商城模块真正实现时，网络按钮的页面 Controller 负责：

```text
接收点击
尝试进入 Busy
发送 Command
```

操作顺序：Controller 收到 `Clicked`，调用 `TryBeginBusy()`；成功后发送该业务的 Command。Command 调用对应 System，System 使用网络 Utility 发送请求。这个顺序只是模块边界，具体类名和参数由未来商城模块确定。

## 八、请求结果和 Busy 恢复

业务 System 完成请求后，通过事件通知 Controller。事件至少携带**请求关联 ID**和完成结果；Controller 仅在关联 ID 与自己发起的操作匹配时调用 `EndBusy()`。只写一个 `Success` 字段会使多个购买同时发生时解锁错按钮。

成功、业务失败、网络异常、超时和取消都属于“本次请求已结束”，都要走到解锁逻辑。页面关闭或对象销毁时也要解除事件监听；如果网络请求还在运行，后续模块还需定义取消或忽略迟到结果的规则。

页面不在这里写网络错误文本，也不在这里创建 Toast。

## 九、错误消息的统一处理

错误提示不属于具体商城页面：

```text
NetworkUtility
    产生传输错误或超时结果

业务 System
    产生业务错误码

统一错误消息服务
    ErrorCode 转换为本地化消息

UIToast / UIPopup
    显示最终提示
```

页面无需为每种错误各写一个 `Show...Message()` 方法。

本篇只要求页面结束 `Busy`。统一错误消息服务将在 UI Toast 模块中单独实现。

## 十、为什么不默认设置点击间隔

固定点击间隔只能表示：

```text
一小段时间内不接受第二次点击
```

它不能表示：

```text
服务器响应前一直禁止重复提交
```

所以本组件不设置默认点击间隔。网络操作使用 `Busy`，特殊的本地冷却操作使用 `Cooldown`。

## 十一、MMORPG 的最终防重复边界

客户端 `Busy` 只改善交互体验，不能作为服务器安全保证。

服务器仍然需要：

```text
校验玩家权限
校验货币和物品数量
校验请求是否过期
校验请求是否重复
使用 requestId 保证幂等
```

如果购买按钮和快捷购买按钮操作同一件商品，两个按钮还应共享业务 System 的请求状态。只锁定两个独立的 UI 按钮仍然不够。

## 十二、Prefab 配置

```text
BuyButton
├── UnityEngine.UI.Button
└── GameFramework.UIButton
```

配置步骤：

```text
1. 根物体添加 Unity Button。
2. 同一个根物体添加 UIButton。
3. 需要点击音效时配置完整 Key；不需要时让 Group 和 Cue 保持空白。
4. Controller 订阅 Clicked。
5. 网络操作开始前调用 TryBeginBusy。
6. 成功、失败、超时和取消时调用 EndBusy。
7. 页面关闭时解除事件。
```

## 十三、验证清单

```text
1. Normal 状态点击只触发一次 Clicked。
2. Busy 状态再次点击不会触发业务。
3. Disabled 状态不能点击。
4. Cooldown 状态不能点击。
5. 请求成功后按钮恢复。
6. 请求失败后按钮恢复。
7. 请求超时后按钮恢复。
8. 请求异常后按钮恢复。
9. 页面关闭后不会残留事件监听。
10. 页面不直接修改 Model 的业务状态。
11. 网络业务由 Command 进入 System。
12. 错误提示不重复写在每个页面中。
13. 空 AudioCueKey 点击时不发出 SoundRequested。
14. 完整 AudioCueKey 点击时发出 SoundRequested。
```

## 十四、本篇完成后的职责结果

```text
UIButton
    统一按钮交互和 Busy 状态

ViewController
    接收按钮事件、发送 Command、监听 Event

Command
    描述一次业务操作

System
    执行业务流程

Model
    保存业务数据

Utility
    提供网络、音频和其他基础设施

UIMessageService
    后续统一处理网络和业务错误提示
```

这样背包、商城、任务和装备页面可以复用同一个 `UIButton`，同时保持当前简易 QFramework 的层级规则。
