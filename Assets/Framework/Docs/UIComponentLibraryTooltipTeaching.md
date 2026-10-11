# UI 组件库下一步：把 Concave Tooltip 接入框架

## 当前目标

按钮迁移已经完成。下一步处理反馈组件中的 Tooltip。

本次只做组件本身的框架化，不做示例场景、不做运行验收、不新增业务页面。

目标结构：

```text
Concave Tooltip Prefab
    ├── 保留布局、背景、箭头和 TMP 字体表现
    ├── 使用 UITooltip 作为统一内容和显示入口
    └── 由调用方传入标题、正文和显示状态
```

## 一、当前 Tooltip Prefab 的职责

基础 Prefab：

```text
Assets/Framework/UI/ComponentLibrary/Concave/Feedback/Tooltips/Tooltip.prefab
```

方向变体：

```text
Tooltip - Top.prefab
Tooltip - Bottom.prefab
Tooltip - Left.prefab
Tooltip - Right.prefab
```

这些变体只负责箭头方向和布局位置，不应该各自拥有一套业务脚本。保留它们，因为它们代表不同的视觉结构；后续只给它们挂同一个 `UITooltip`。

当前基础 Prefab 已经有：

- `HorizontalLayoutGroup` 或 `VerticalLayoutGroup`。
- `ContentSizeFitter`，用于根据 TMP 文本自动调整尺寸。
- `CanvasGroup`，用于控制显示透明度和射线状态。
- TMP 标题和正文对象。

因此第一版不重新实现布局，也不把 Tooltip 改成页面。Tooltip 是一个短时反馈组件，不应进入 `UINavigationService` 的页面返回栈。

## 二、新增框架组件

新建文件：

```text
Assets/Framework/Runtime/UI/Components/UITooltip.cs
```

完整代码：

```csharp
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 通用 Tooltip 的表现组件。
    ///
    /// 这个组件只负责三件事：
    /// 1. 接收调用方传入的文本；
    /// 2. 控制 CanvasGroup 的显示和隐藏；
    /// 3. 播放短暂的淡入淡出动画。
    ///
    /// 它不负责决定什么时候显示，也不读取背包、任务或角色数据。
    /// 具体业务 Controller 只需要准备字符串并调用 Show/Hide。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UITooltip : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField]
        private TMP_Text mTitle;

        [SerializeField]
        private TMP_Text mBody;

        [Header("Animation")]
        [SerializeField]
        private float mFadeDuration = 0.12f;

        private CanvasGroup mCanvasGroup;
        private Tween mFadeTween;

        /// <summary>
        /// 当前 Tooltip 是否处于显示状态。
        /// </summary>
        public bool IsVisible { get; private set; }

        private void Awake()
        {
            mCanvasGroup = GetComponent<CanvasGroup>();
            HideImmediate();
        }

        private void OnDestroy()
        {
            // Tooltip 可能由页面销毁，销毁前停止 DOTween，避免 Tween 继续访问已销毁对象。
            mFadeTween?.Kill();
        }

        /// <summary>
        /// 设置标题和正文。
        /// 空标题会隐藏标题对象，但不会影响正文显示。
        /// </summary>
        public void SetContent(string title, string body)
        {
            if (mTitle != null)
            {
                mTitle.text = title ?? string.Empty;
                mTitle.gameObject.SetActive(!string.IsNullOrEmpty(title));
            }

            if (mBody != null)
            {
                mBody.text = body ?? string.Empty;
            }
        }

        /// <summary>
        /// 设置内容并显示 Tooltip。
        /// Tooltip 的位置由对应方向 Prefab 的 RectTransform 和调用方决定。
        /// </summary>
        public void Show(string title, string body)
        {
            SetContent(title, body);
            Show();
        }

        /// <summary>
        /// 显示已经设置好内容的 Tooltip。
        /// </summary>
        public void Show()
        {
            mFadeTween?.Kill();
            IsVisible = true;
            gameObject.SetActive(true);
            mCanvasGroup.blocksRaycasts = false;
            mCanvasGroup.interactable = false;

            mFadeTween = mCanvasGroup
                .DOFade(1f, mFadeDuration)
                .SetUpdate(true);
        }

        /// <summary>
        /// 隐藏 Tooltip，但保留实例，方便下一次复用。
        /// </summary>
        public void Hide()
        {
            if (!gameObject.activeSelf)
            {
                IsVisible = false;
                return;
            }

            mFadeTween?.Kill();
            IsVisible = false;
            mCanvasGroup.blocksRaycasts = false;
            mCanvasGroup.interactable = false;

            mFadeTween = mCanvasGroup
                .DOFade(0f, mFadeDuration)
                .SetUpdate(true)
                .OnComplete(() => gameObject.SetActive(false));
        }

        /// <summary>
        /// 初始化时立即隐藏，不播放退出动画。
        /// </summary>
        private void HideImmediate()
        {
            mFadeTween?.Kill();
            IsVisible = false;
            mCanvasGroup.alpha = 0f;
            mCanvasGroup.blocksRaycasts = false;
            mCanvasGroup.interactable = false;
            gameObject.SetActive(false);
        }
    }
}
```

### 代码边界说明

- 使用 `TMP_Text`，所以 `TextMeshProUGUI` 和其他 TMP 文本实现都能绑定。
- Tooltip 不阻挡底层输入，因为它是提示信息，不是弹窗。
- `Hide()` 只隐藏对象，不销毁对象，后续可以由 Tooltip 管理器复用实例。
- `SetUpdate(true)` 让 UI 淡入淡出不受游戏暂停影响。
- 内容、生命周期和动画都集中在一个组件中，业务页面不需要直接操作 TMP 和 CanvasGroup。

## 三、改造基础 Tooltip Prefab

打开：

```text
Assets/Framework/UI/ComponentLibrary/Concave/Feedback/Tooltips/Tooltip.prefab
```

在根对象上完成以下修改：

1. 保留 `CanvasGroup`。
2. 保留布局组件和 `ContentSizeFitter`。
3. 保留背景、箭头、标题和正文的美术资源。
4. 删除资源包中只负责演示的旧 Tooltip 行为脚本，如果 Inspector 中存在。
5. 添加 `UITooltip`。
6. 将标题 TMP 对象拖到 `M Title`。
7. 将正文 TMP 对象拖到 `M Body`。
8. `Fade Duration` 保留默认值 `0.12`，后续再按主题调整。

不要把 Tooltip 根对象挂 `UIPage`。它不是页面，不参与页面导航，也不应该拥有自己的返回行为。

## 四、改造四个方向变体

对下面四个 Prefab 做同样处理：

```text
Tooltip - Top.prefab
Tooltip - Bottom.prefab
Tooltip - Left.prefab
Tooltip - Right.prefab
```

每个变体只修改组件引用，不修改自己的布局结构：

```text
根对象
    ├── 保留原有方向布局
    ├── 保留原有箭头 Sprite
    ├── 添加 UITooltip
    ├── M Title 绑定标题 TMP
    └── M Body 绑定正文 TMP
```

这样业务方选择“上方提示”或“右侧提示”时，只是在选择表现变体，调用代码仍然只面对 `UITooltip`。

## 五、业务层的正确使用方式

业务 Controller 只保存 Tooltip 引用并传入内容：

```csharp
using UnityEngine;

namespace GameFramework.Examples
{
    /// <summary>
    /// 这里只展示调用边界，不要求现在把它接入示例场景。
    /// </summary>
    public sealed class ItemTooltipController : MonoBehaviour
    {
        [SerializeField]
        private UITooltip mTooltip;

        public void ShowItem(string itemName, string description)
        {
            mTooltip.Show(itemName, description);
        }

        public void HideItem()
        {
            mTooltip.Hide();
        }
    }
}
```

业务脚本不应该这样写：

```csharp
// 不要在每个业务脚本中重复查找 TMP 和 CanvasGroup。
GetComponentInChildren<TMP_Text>().text = description;
GetComponent<CanvasGroup>().alpha = 1f;
```

重复查找和重复动画会让每个业务页面都重新实现一遍 Tooltip 生命周期，组件库就失去了意义。

## 六、本次不做的内容

本阶段明确暂缓：

- 不创建示例场景。
- 不写运行验收脚本。
- 不接入背包、任务或商店业务。
- 不实现全局 Tooltip Manager。
- 不实现鼠标跟随、屏幕边界自动翻转。
- 不把 Tooltip 放入 UI 导航栈。

等基础 `UITooltip` 在组件层完成后，再决定是否需要一个统一的 Tooltip 服务。当前直接增加服务会提前引入对象创建、定位和复用策略，暂时没有必要。

## 七、完成后的组件库状态

完成本篇后，组件库将形成两个已经框架化的基础组件：

```text
UIButton
    ├── 统一点击事件
    ├── Busy 状态
    └── AudioCueKey 音效请求

UITooltip
    ├── TMP 内容设置
    ├── 显示和隐藏生命周期
    └── DOTween 淡入淡出表现
```

下一个组件再考虑 `Notification`。它比 Tooltip 更接近临时消息系统，需要处理持续时间、队列和对象复用，所以应放在 Tooltip 之后实现。

