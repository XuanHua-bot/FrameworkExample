# UI 组件库实作 01：UIButton（新版）

本篇是新的按钮组件教学文档，不修改旧的 `UIButtonTeaching.md`。

目标：在 Unity 原生 `Button` 之上增加统一的点击间隔、点击事件、音效和 DOTween 按压反馈。按钮不处理具体业务，也不直接发送网络请求。

## 一、文件位置

新建目录：

```text
Assets/Framework/Runtime/UI/Components/
```

新建文件：

```text
UIButton.cs
UIButtonEffect.cs
```

## 二、UIButton

文件：`Assets/Framework/Runtime/UI/Components/UIButton.cs`

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButton : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float mClickInterval = 0.3f;

        [SerializeField]
        private bool mPlayClickSound = true;

        [SerializeField]
        private AudioCueKey mClickSound;

        private Button mButton;
        private float mNextClickTime;
        private bool mInteractableBeforeWaiting;

        public event Action Clicked;
        public event Action<AudioCueKey> SoundRequested;

        public bool Interactable => mButton != null && mButton.interactable;

        private void Awake()
        {
            mButton = GetComponent<Button>();
        }

        private void OnEnable()
        {
            mButton.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            mButton.onClick.RemoveListener(HandleClick);
        }

        public void SetInteractable(bool value)
        {
            mButton.interactable = value;
        }

        public void BeginWaiting()
        {
            mInteractableBeforeWaiting = mButton.interactable;
            mButton.interactable = false;
        }

        public void EndWaiting()
        {
            mButton.interactable = mInteractableBeforeWaiting;
        }

        private void HandleClick()
        {
            if (Time.unscaledTime < mNextClickTime) return;
            mNextClickTime = Time.unscaledTime + mClickInterval;

            if (mPlayClickSound)
                SoundRequested?.Invoke(mClickSound);

            Clicked?.Invoke();
        }
    }
}
```

### 代码说明

`UIButton` 仍然使用 Unity 原生 `Button` 的射线检测、键盘导航和 `interactable` 状态。这个组件只增加项目统一规则。

`mClickInterval` 使用 `Time.unscaledTime`，因此游戏暂停时，按钮仍然可以按照真实 UI 时间限制连续点击。

`AudioCueKey` 由两部分组成：

```text
资源组：Audio/CommonUI
组内音频：Click
```

常用音频不要在每个按钮上手写构造代码。实际项目应建立使用端的静态键：

```csharp
public static class ExampleAudioKeys
{
    public static readonly AudioCueKey UiClick =
        new AudioCueKey("Audio/CommonUI", "Click");
}
```

然后在 Inspector 中把 `mClickSound` 配置为同样的两个字段，或者在项目自己的按钮预制体中统一配置。

## 三、UIButtonEffect

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
        [SerializeField, Min(0.01f)] private float mPressedScale = 0.94f;
        [SerializeField, Min(0f)] private float mDuration = 0.08f;

        private UIButton mButton;
        private Vector3 mInitialScale;
        private Tween mTween;

        private void Awake()
        {
            mButton = GetComponent<UIButton>();
            if (mTarget == null) mTarget = transform;
            mInitialScale = mTarget.localScale;
        }

        private void OnEnable()
        {
            mButton.Clicked += Play;
        }

        private void OnDisable()
        {
            mButton.Clicked -= Play;
            ResetEffect();
        }

        private void Play()
        {
            mTween?.Kill();
            mTarget.localScale = mInitialScale;
            mTween = mTarget.DOScale(mInitialScale * mPressedScale, mDuration)
                .SetLoops(2, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private void ResetEffect()
        {
            mTween?.Kill();
            mTween = null;
            if (mTarget != null) mTarget.localScale = mInitialScale;
        }
    }
}
```

组件禁用、页面关闭或对象回收时必须停止 Tween，并恢复初始缩放，避免下次复用时保留上一次的动画状态。

## 四、页面使用

页面脚本只订阅 `UIButton.Clicked`：

```csharp
public sealed class ExamplePage : UIPage
{
    [SerializeField] private UIButton mConfirmButton;

    public override void OnOpen()
    {
        mConfirmButton.Clicked += OnConfirmClicked;
        mConfirmButton.SoundRequested += OnSoundRequested;
    }

    public override void OnClose()
    {
        mConfirmButton.Clicked -= OnConfirmClicked;
        mConfirmButton.SoundRequested -= OnSoundRequested;
    }

    private void OnConfirmClicked()
    {
        // 这里只处理当前页面的业务动作。
    }

    private void OnSoundRequested(AudioCueKey key)
    {
        ExampleArchitecture.Interface.GetUtility<IAudioService>().Play(key);
    }
}
```

不要同时把业务逻辑挂在原生 `Button.onClick` 和 `UIButton.Clicked` 上，否则一次点击会执行两次。

## 五、Prefab 配置

在按钮根物体上保留：

```text
Button
UIButton
UIButtonEffect（可选）
```

配置顺序：

1. 根物体必须已有 Unity `Button`。
2. 添加 `UIButton`。
3. 设置点击间隔。
4. 配置 `mClickSound`，指向 `Audio/CommonUI` 组中的 `Click`。
5. 需要按压缩放时添加 `UIButtonEffect`。
6. 页面业务改为监听 `UIButton.Clicked`。

## 六、当前版本的边界

本篇暂不加入双击、长按和网络请求状态。这些功能需要在基础按钮确认可用后，再分别实现，避免一个按钮脚本同时承担过多职责。

完成本篇后，下一步实现 `UIAsyncButton`，用于点击后等待服务器结果并阻止重复提交。
