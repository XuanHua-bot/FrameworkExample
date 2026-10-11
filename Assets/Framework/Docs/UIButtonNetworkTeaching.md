# UI 组件库实作 01：适配网络游戏的统一 UIButton

本篇是新的教学文档，不修改旧的 `UIButtonTeaching.md` 和 `UIButtonTeachingV2.md`。

本篇实现一个统一的 `UIButton`，用于普通 UI 和网络操作 UI。制作页面时始终使用同一个按钮组件，不需要根据“是否联网”更换脚本。

## 一、要解决的问题

Unity 原生 `Button` 已经负责：

```text
射线检测
键盘和手柄导航
interactable
onClick
```

但是 MMORPG 项目还需要统一处理：

```text
点击事件
点击音效
按压动效
请求处理中锁定
请求完成后恢复
失败和异常后的恢复
按钮禁用
```

`UIButton` 只负责按钮交互状态，不负责购买、领取、强化等具体业务，也不直接实现网络协议。

## 二、组件状态

文件：`Assets/Framework/Runtime/UI/Components/UIButtonState.cs`

```csharp
namespace GameFramework
{
    /// <summary>
    /// 统一按钮的交互状态。
    /// </summary>
    public enum UIButtonState
    {
        /// <summary>
        /// 按钮可以接受点击。
        /// </summary>
        Normal,

        /// <summary>
        /// 按钮正在等待业务层或服务器返回结果。
        /// 进入此状态后，按钮不再接受重复点击。
        /// </summary>
        Busy,

        /// <summary>
        /// 按钮被业务条件禁用。
        /// 例如玩家等级不足、没有货币或当前页面不可操作。
        /// </summary>
        Disabled,

        /// <summary>
        /// 按钮处于业务要求的冷却时间内。
        /// 冷却和网络请求等待是两种不同状态。
        /// </summary>
        Cooldown
    }
}
```

## 三、统一 UIButton

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
    /// 该组件建立在 Unity 原生 Button 之上，负责统一点击、音效请求、
    /// Interactable 状态和网络请求期间的 Busy 锁定。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButton : MonoBehaviour
    {
        /// <summary>
        /// Unity 原生 Button 组件。
        /// 射线检测、键盘导航和基础 Interactable 由它完成。
        /// </summary>
        private Button mButton;

        /// <summary>
        /// Busy 之前的交互状态。
        /// 请求结束后需要恢复到请求开始前的状态，而不是无条件设为可点击。
        /// </summary>
        private bool mInteractableBeforeBusy;

        /// <summary>
        /// 当前是否处于 Busy 状态。
        /// </summary>
        private bool mIsBusy;

        /// <summary>
        /// 是否在点击后请求播放点击音效。
        /// </summary>
        [SerializeField]
        private bool mPlayClickSound = true;

        /// <summary>
        /// 当前按钮使用的点击音效 Key。
        /// </summary>
        [SerializeField]
        private AudioCueKey mClickSound;

        /// <summary>
        /// 当前是否允许点击。
        /// </summary>
        public bool Interactable => mButton != null && mButton.interactable;

        /// <summary>
        /// 当前是否正在等待一次业务操作完成。
        /// </summary>
        public bool IsBusy => mIsBusy;

        /// <summary>
        /// 当前按钮状态。
        /// </summary>
        public UIButtonState State { get; private set; } = UIButtonState.Normal;

        /// <summary>
        /// 按钮通过 Unity 原生 Button 成功接收一次点击后触发。
        /// 页面或控制器应在这里绑定本地业务动作。
        /// </summary>
        public event Action Clicked;

        /// <summary>
        /// 按钮点击后请求播放音效时触发。
        /// 音频服务由页面或项目层订阅，组件本身不依赖具体音频实现。
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        /// <summary>
        /// Unity 生命周期初始化方法。
        /// 获取同一个物体上的原生 Button 组件，并设置初始状态。
        /// </summary>
        private void Awake()
        {
            mButton = GetComponent<Button>();
            State = mButton.interactable
                ? UIButtonState.Normal
                : UIButtonState.Disabled;
        }

        /// <summary>
        /// Unity 生命周期启用方法。
        /// 只在组件启用期间监听原生 Button 的点击事件。
        /// </summary>
        private void OnEnable()
        {
            mButton.onClick.AddListener(HandleUnityClick);
        }

        /// <summary>
        /// Unity 生命周期禁用方法。
        /// 解除监听，避免页面或 Prefab 复用时重复注册回调。
        /// </summary>
        private void OnDisable()
        {
            mButton.onClick.RemoveListener(HandleUnityClick);
        }

        /// <summary>
        /// 设置按钮是否可以被用户操作。
        ///
        /// 该方法用于永久或业务条件上的禁用，例如条件不足。
        /// 网络请求期间应该使用 TryBeginBusy，而不是直接调用此方法。
        /// </summary>
        /// <param name="interactable">true 表示恢复可交互，false 表示禁用。</param>
        public void SetInteractable(bool interactable)
        {
            if (mIsBusy) return;

            mButton.interactable = interactable;
            State = interactable
                ? UIButtonState.Normal
                : UIButtonState.Disabled;
        }

        /// <summary>
        /// 尝试进入 Busy 状态。
        ///
        /// 网络请求开始前调用此方法。只有第一个调用者能成功进入 Busy，
        /// 后续重复调用会返回 false，从而避免重复提交同一操作。
        /// </summary>
        /// <returns>
        /// 成功进入 Busy 返回 true；按钮已经 Busy、Disabled 或 Cooldown 时返回 false。
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
        /// 结束 Busy 状态并恢复按钮。
        ///
        /// 网络请求成功、失败、超时或取消时都必须调用。
        /// 推荐放在 try/finally 的 finally 中，避免异常导致按钮永久锁死。
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
        /// 设置冷却状态。
        ///
        /// 本篇不实现自动计时器。冷却时间通常由具体业务规则决定，
        /// 业务层在冷却结束后调用 ClearCooldown。
        /// </summary>
        public void BeginCooldown()
        {
            if (mIsBusy) return;

            mButton.interactable = false;
            State = UIButtonState.Cooldown;
        }

        /// <summary>
        /// 结束冷却并恢复可交互状态。
        /// </summary>
        public void ClearCooldown()
        {
            if (mIsBusy) return;

            mButton.interactable = true;
            State = UIButtonState.Normal;
        }

        /// <summary>
        /// 接收 Unity 原生 Button 的点击回调。
        /// 只有 Normal 状态可以继续向页面广播点击事件。
        /// </summary>
        private void HandleUnityClick()
        {
            if (State != UIButtonState.Normal || !mButton.interactable)
            {
                return;
            }

            Clicked?.Invoke();

            if (mPlayClickSound)
            {
                SoundRequested?.Invoke(mClickSound);
            }
        }
    }
}
```

## 四、音效连接

按钮已经通过 `mClickSound` 保存音频 Key，不需要每次点击时 `new AudioCueKey`。

页面或项目层订阅音效：

```csharp
private void OnButtonSoundRequested(AudioCueKey key)
{
    var audio = ExampleArchitecture.Interface.GetUtility<IAudioService>();
    audio.Play(key);
}
```

这样 `UIButton` 不需要知道 `AudioService` 的具体获取方式，也不会把项目架构依赖写死在基础控件中。

## 五、普通按钮的使用

普通关闭按钮、返回按钮和 Tab 按钮只需要订阅 `Clicked`：

```csharp
/// <summary>
/// 示例页面中的按钮控制器。
/// </summary>
public sealed class ExamplePage : UIPage
{
    /// <summary>
    /// 页面上的关闭按钮。
    /// </summary>
    [SerializeField]
    private UIButton mCloseButton;

    /// <summary>
    /// 页面打开时注册按钮事件。
    /// </summary>
    public override void OnOpen()
    {
        mCloseButton.Clicked += ClosePage;
    }

    /// <summary>
    /// 页面关闭前解除按钮事件。
    /// </summary>
    public override void OnClose()
    {
        mCloseButton.Clicked -= ClosePage;
    }

    /// <summary>
    /// 执行页面关闭业务。
    /// </summary>
    private void ClosePage()
    {
        this.GetUtility<IUGUIService>().Close(this);
    }
}
```

普通按钮不需要设置点击间隔，也不需要进入 Busy。

## 六、网络按钮的使用

网络操作的完整流程应该由页面或业务控制器负责：

```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace GameFramework.Examples
{
    /// <summary>
    /// 示例商城页面。
    /// 页面负责调用网络业务，按钮只负责交互状态。
    /// </summary>
    public sealed class ShopPage : UIPage
    {
        /// <summary>
        /// 购买按钮。
        /// </summary>
        [SerializeField]
        private UIButton mBuyButton;

        /// <summary>
        /// 页面打开时绑定购买事件。
        /// </summary>
        public override void OnOpen()
        {
            mBuyButton.Clicked += BuyItem;
        }

        /// <summary>
        /// 页面关闭时解除购买事件。
        /// </summary>
        public override void OnClose()
        {
            mBuyButton.Clicked -= BuyItem;
        }

        /// <summary>
        /// 开始一次购买操作。
        /// 使用 try/finally 确保网络失败、超时或异常时按钮能够恢复。
        /// </summary>
        private async void BuyItem()
        {
            if (!mBuyButton.TryBeginBusy()) return;

            try
            {
                var result = await SendBuyRequest();

                if (result)
                {
                    RefreshShopAndInventory();
                }
                else
                {
                    ShowBuyFailedMessage();
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowNetworkErrorMessage();
            }
            finally
            {
                mBuyButton.EndBusy();
            }
        }

        /// <summary>
        /// 示例网络请求。
        /// 实际项目中应替换成项目自己的网络服务调用。
        /// </summary>
        private async Task<bool> SendBuyRequest()
        {
            await Task.Delay(500);
            return true;
        }

        /// <summary>
        /// 购买成功后刷新商城和背包显示。
        /// </summary>
        private void RefreshShopAndInventory()
        {
        }

        /// <summary>
        /// 显示服务器返回的业务失败信息。
        /// </summary>
        private void ShowBuyFailedMessage()
        {
        }

        /// <summary>
        /// 显示网络异常或请求超时信息。
        /// </summary>
        private void ShowNetworkErrorMessage()
        {
        }
    }
}
```

## 七、为什么不能只使用点击间隔

点击间隔只解决时间范围内的重复点击：

```text
0.3 秒内再次点击无效
```

它不能解决网络请求等待：

```text
第一次请求用了 2 秒
玩家第 1 秒再次点击
```

因此网络按钮应使用 `Busy` 状态。点击间隔只作为特殊控件的可选扩展，不作为基础 `UIButton` 的默认逻辑。

## 八、MMORPG 项目的服务器边界

客户端按钮锁定不能代替服务器校验。服务器仍然需要：

```text
检查玩家权限
检查资源数量
检查物品状态
检查请求是否重复
检查请求是否过期
```

例如购买请求应带有唯一请求编号：

```text
BuyItemRequest
├── requestId
├── playerId
├── itemId
└── quantity
```

服务器应保证同一个 `requestId` 重复到达时不会重复扣除货币。

客户端 `UIButton.Busy` 解决用户体验和常见重复点击；服务器幂等处理才是最终业务保证。

## 九、多个按钮触发同一业务

如果以下两个按钮执行同一个购买操作：

```text
商城购买按钮
快捷购买按钮
```

只锁定其中一个按钮不够。两个入口应共享业务层的请求锁：

```csharp
if (!mPurchaseOperation.TryBegin()) return;

try
{
    await SendPurchaseRequest();
}
finally
{
    mPurchaseOperation.End();
}
```

按钮锁定属于界面层，业务请求锁属于业务层，两者不能混为一谈。

## 十、Prefab 配置步骤

按钮物体结构：

```text
BuyButton
├── Button
└── UIButton
```

配置步骤：

```text
1. 根物体添加 Unity Button。
2. 同一个根物体添加 UIButton。
3. 按钮普通点击逻辑订阅 Clicked。
4. 网络操作在 Clicked 回调中调用 TryBeginBusy。
5. 请求成功、失败、超时和取消都调用 EndBusy。
6. 页面关闭时解除 Clicked 事件。
```

按压缩放、颜色变化等表现可以继续作为独立的 `UIButtonEffect` 添加，不改变 `UIButton` 的网络逻辑。

## 十一、验证清单

```text
1. 普通按钮点击一次只触发一次 Clicked。
2. Busy 状态下再次点击不会触发业务。
3. 请求成功后按钮恢复。
4. 请求失败后按钮恢复。
5. 请求抛出异常后按钮恢复。
6. 请求超时后按钮恢复。
7. 页面关闭后重新打开，不会重复触发旧事件。
8. 两个入口触发同一业务时，业务层不会并发提交。
9. 服务器重复收到同一个 requestId 时，不会重复执行扣款或发奖。
```

## 十二、后续扩展顺序

本篇完成后，组件库按以下顺序继续：

```text
UIButton
    ↓
UIButtonEffect
    ↓
UIItemView
    ↓
UIItemList
    ↓
UITabGroup
    ↓
UIVirtualGrid
```

背包、商城和任务列表都复用同一个 `UIButton`，不为每个业务页面重新制作按钮脚本。
