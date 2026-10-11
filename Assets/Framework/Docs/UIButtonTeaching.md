# UI 组件库实作 01：UIButton

## 要实现的结果

本篇完成组件库的第一个可用控件。把 `UIButton` 与原生 `Button` 挂在同一个物体上，页面订阅 `UIButton.Clicked`，就能得到统一的点击间隔、请求等待锁定和音效请求。`UIButtonEffect` 是可选的按压缩放动效。`UIAsyncButton` 用于等待联网操作，成功或失败后解除锁定。

本篇只新增三个脚本，不修改 `UIPage`、`UGUIService`、现有页面或 Prefab。项目已接入 DOTween，本篇的按钮动效直接使用它。先在一个新建的测试按钮上验证，再决定是否逐个迁移旧按钮。

## 一、先建立目录

在 Unity 项目中建立：

```text
Assets/Framework/Runtime/UI/Components/
```

这个目录与现有 `UGUI` 页面管理目录并列。按钮属于页面内部的组件，不由 `UGUIService` 创建或释放。

## 二、写 UIButton

文件：`Assets/Framework/Runtime/UI/Components/UIButton.cs`

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameFramework
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButton : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Min(0f)] private float mClickInterval = 0.3f;
        [SerializeField] private string mClickSoundId;

        private Button mButton;
        private float mNextClickTime;
        private bool mBusy;
        private bool mPressed;
        private bool mAllowed;

        public event Action Clicked;
        public event Action Pressed;
        public event Action Released;
        public event Action<string> SoundRequested;

        public bool IsBusy => mBusy;
        public bool IsInteractable => mButton != null && mButton.IsInteractable();

        private void Awake()
        {
            mButton = GetComponent<Button>();
            mAllowed = mButton.interactable;
        }

        private void OnEnable()
        {
            mButton.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            mButton.onClick.RemoveListener(HandleClick);
            Release();
        }

        public void SetBusy(bool busy)
        {
            mBusy = busy;
            mButton.interactable = mAllowed && !busy;
            if (busy) Release();
        }

        public void SetInteractable(bool interactable)
        {
            mAllowed = interactable;
            mButton.interactable = mAllowed && !mBusy;
            if (!interactable) Release();
        }

        private void HandleClick()
        {
            if (mBusy || !mButton.IsInteractable() ||
                Time.unscaledTime < mNextClickTime)
                return;

            mNextClickTime = Time.unscaledTime + mClickInterval;

            if (!string.IsNullOrEmpty(mClickSoundId))
                SoundRequested?.Invoke(mClickSoundId);

            Clicked?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable || mPressed) return;
            mPressed = true;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void Release()
        {
            if (!mPressed) return;
            mPressed = false;
            Released?.Invoke();
        }
    }
}
```

这里仍然使用原生 `Button` 的射线、导航、键盘提交和 `interactable` 能力；新脚本集中管理项目自己的点击规则。`Time.unscaledTime` 保证游戏暂停时 UI 点击间隔仍正常工作。`SoundRequested` 只给出音效 ID，因为当前框架没有通用音频服务。

注意：按钮 Inspector 中原有的 `Button.onClick` 持久监听会绕过 `UIButton` 的点击间隔。接入后，业务点击逻辑应订阅 `UIButton.Clicked`，清理旧的 `Button.onClick` 业务监听。同一个按钮的可交互状态应统一通过 `UIButton.SetInteractable` 管理；等待结束时会恢复等待前的允许状态。

## 三、写可选的按压动效

文件：`Assets/Framework/Runtime/UI/Components/UIButtonEffect.cs`

```csharp
using DG.Tweening;
using UnityEngine;

namespace GameFramework
{
    [RequireComponent(typeof(UIButton))]
    public sealed class UIButtonEffect : MonoBehaviour
    {
        [SerializeField] private Transform mTarget;
        [SerializeField, Range(0.9f, 1f)] private float mPressedScale = 0.97f;
        [SerializeField, Min(0f)] private float mPressDuration = 0.07f;
        [SerializeField, Min(0f)] private float mReleaseDuration = 0.12f;

        private UIButton mButton;
        private Vector3 mOriginalScale;
        private Tweener mTween;

        private void Awake()
        {
            mButton = GetComponent<UIButton>();
            if (mTarget == null) mTarget = transform;
            mOriginalScale = mTarget.localScale;
        }

        private void OnEnable()
        {
            mButton.Pressed += OnPressed;
            mButton.Released += OnReleased;
        }

        private void OnDisable()
        {
            mButton.Pressed -= OnPressed;
            mButton.Released -= OnReleased;
            mTween?.Kill();
            mTween = null;
            mTarget.localScale = mOriginalScale;
        }

        private void OnPressed()
        {
            AnimateTo(mOriginalScale * mPressedScale, mPressDuration, Ease.OutQuad);
        }

        private void OnReleased()
        {
            AnimateTo(mOriginalScale, mReleaseDuration, Ease.OutCubic);
        }

        private void AnimateTo(Vector3 targetScale, float duration, Ease ease)
        {
            mTween?.Kill();
            mTween = mTarget.DOScale(targetScale, duration)
                .SetEase(ease)
                .SetUpdate(true);
        }
    }
}
```

`mTarget` 可以指向按钮的视觉子节点，避免缩放整个布局节点造成排版抖动。按压幅度小、回弹快，是本项目参考《原神》交互节奏选用的方向；`0.97 / 0.07 / 0.12` 是建议起点，不是该游戏的官方参数。以后可在这一组件中增加轻微悬停高亮，但先用实际按钮验证手感。禁用组件时会 Kill 当前 Tween 并恢复原始缩放，防止复用后的按钮保留按压状态。

## 四、写联网等待组件

文件：`Assets/Framework/Runtime/UI/Components/UIAsyncButton.cs`

```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace GameFramework
{
    [RequireComponent(typeof(UIButton))]
    public sealed class UIAsyncButton : MonoBehaviour
    {
        private UIButton mButton;

        public event Action<Exception> Failed;

        private void Awake()
        {
            mButton = GetComponent<UIButton>();
        }

        public async Task<bool> RunAsync(Func<Task> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (mButton.IsBusy) return false;

            mButton.SetBusy(true);
            try
            {
                await operation();
                return true;
            }
            catch (Exception error)
            {
                Failed?.Invoke(error);
                return false;
            }
            finally
            {
                if (mButton != null) mButton.SetBusy(false);
            }
        }
    }
}
```

这个组件不发送 HTTP 请求，也不判断服务器业务码。业务层传入的 `operation` 应在服务器明确返回成功时完成；失败时抛出异常，或由业务层自行转换为失败结果。等待中 `UIButton` 不再接受点击。`RunAsync` 返回 `false` 表示请求失败，或者前一个请求尚未结束。

重要边界：`UIAsyncButton` 只阻止本地重复触发，不能代替服务器端的幂等校验。如果页面在请求中关闭，请求取消也应由业务层的网络 API 负责；这里没有假设当前框架已经提供取消令牌或网络服务。

## 五、在页面中使用

下面是接线示例，放在一个你自己创建的测试页面里；不要照着修改现有 `ExampleScenePage`。

```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;

public sealed class ButtonDemo : MonoBehaviour
{
    [SerializeField] private GameFramework.UIButton mButton;
    [SerializeField] private GameFramework.UIAsyncButton mAsyncButton;

    private void OnEnable()
    {
        mButton.Clicked += OnClicked;
        mButton.SoundRequested += OnSoundRequested;
        mAsyncButton.Failed += OnRequestFailed;
    }

    private void OnDisable()
    {
        mButton.Clicked -= OnClicked;
        mButton.SoundRequested -= OnSoundRequested;
        mAsyncButton.Failed -= OnRequestFailed;
    }

    private async void OnClicked()
    {
        var success = await mAsyncButton.RunAsync(RequestAsync);
        if (success) Debug.Log("请求成功");
    }

    private Task RequestAsync()
    {
        // 将来替换为业务层真实的联网请求。
        return Task.Delay(1000);
    }

    private void OnSoundRequested(string soundId)
    {
        // 将来交给项目自己的音频服务；这里先观察事件。
        Debug.Log("点击音效 ID: " + soundId);
    }

    private void OnRequestFailed(Exception error)
    {
        Debug.LogError("请求失败: " + error.Message);
    }
}
```

`Task.Delay` 只是用来观察等待状态，不代表网络实现。真实请求必须保持在业务层。页面关闭后异步操作仍可能完成，因此在正式业务中还需要由业务层按页面生命周期处理取消或忽略结果。

## 六、配置 Prefab

1. 复制一个现有按钮 Prefab，保留原生 `Button`、`Image` 和 TMP 文本。
2. 在按钮根物体加 `UIButton`；按需再加 `UIButtonEffect` 和 `UIAsyncButton`。
3. `UIButtonEffect.mTarget` 指向按钮的视觉子节点；没有子节点时可以留空。
4. 填写 `Click Interval`，例如 `0.3`。需要点击音效时填写 `Click Sound Id`。
5. 清理原生 `Button.onClick` 上的业务监听，再由页面订阅 `UIButton.Clicked`。
6. 将 `ButtonDemo` 的两个引用都拖到同一按钮物体上的组件。

音效和动效相互独立：没有音频服务时按钮照常点击；不加 `UIButtonEffect` 时也照常点击。

## 七、手动验证

1. 连续快速点击：`Clicked` 在间隔内只发生一次。
2. 正常点击：收到 `Clicked`，设置音效 ID 后收到 `SoundRequested`。
3. 按下、移出、松开：视觉对象恢复原始缩放。
4. 触发 `RunAsync` 后的一秒内：按钮不可交互，也不会重复触发请求。
5. 请求完成：按钮恢复可交互。
6. 将 `RequestAsync` 改成抛出异常：触发 `Failed`，按钮仍恢复可交互。
7. 禁用再启用按钮：没有重复订阅或永久缩小。

本篇完成后再做下一篇。下一篇会围绕 `UIButton` 的实际使用结果决定是否补双击、长按和更丰富的状态反馈，不提前把所有交互都塞进同一个脚本。
