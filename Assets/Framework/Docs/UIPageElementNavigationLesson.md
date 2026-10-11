# 已废弃：UIPage 与 UIElement 局部导航

> 这份文档把 QFramework 的 `UIElement`（控件分组概念）误当成了页面内部导航概念，不能继续作为实现依据。
>
> 请改按 [UINavigationServiceLesson.md](UINavigationServiceLesson.md) 实现：导航历史由 `UINavigationService` 统一管理，`UIElement` 仍只用于组织复杂 UI 的子控件。

---

## 当前模块进度（2/3）

当前 UGUI 已经完成：

- `UIPage` 页面加载和关闭。
- `Single` / `Multiple` 页面。
- `Scene` / `Persistent` 生命周期。
- `UGUIRoot` 层级管理。
- 页面实例和 `AssetHandle` 管理。
- 全局 `Stack<UIPage>` 返回栈。

本阶段解决一个新的实际问题：

```text
一个完整 UI 页面内部，还包含多个可以切换的子界面。
```

例如：

```text
AuthPage
├── LoginElement
├── RegisterElement
└── ResetPasswordElement
```

或者：

```text
ActivityPage
├── ActivityHomeElement
├── RegionElement
├── LevelDetailElement
├── MonsterElement
├── ShopElement
└── AchievementElement
```

本阶段只做结构重构和一个局部导航示例，不把所有子界面都注册成全局 `UIPage`。

---

## 一、先确定页面的两级关系

当前系统只有全局页面：

```text
UGUIService
└── UIPage
```

现在扩展为两级：

```text
UGUIService
└── UIPage
    └── UIElementNavigator
        └── UIElement
```

### UIPage

`UIPage` 表示一个完整功能页面或完整功能流程，例如：

- `AuthPage`
- `ActivityPage`
- `SettingsPage`

它继续由 `UGUIService` 管理，负责：

- 资源加载。
- UI 层级。
- Scene / Persistent。
- 页面级生命周期。
- 全局返回栈。

### UIElement

`UIElement` 表示页面内部的子界面，例如：

- `LoginElement`
- `RegisterElement`
- `MonsterElement`
- `ShopElement`

它不进入 `UGUIService`，也不需要 `UIPageAttribute`。

### UIElementNavigator

它只管理某一个 `UIPage` 内部的子界面切换和局部返回。

它不负责：

- 加载全局页面 prefab。
- 管理 `AssetHandle`。
- 操作 `UGUIService` 的字典。
- 处理全局场景生命周期。

---

## 二、为什么不能把每个子界面都做成 UIPage

如果活动流程全部注册成全局页面：

```text
ActivityPage
RegionPage
LevelDetailPage
MonsterPage
ShopPage
AchievementPage
```

那么它们都会进入同一个全局页面系统，产生几个问题：

- 活动内部导航和普通设置页面混在一起。
- 子页面不知道自己属于哪个父页面。
- `UGUIService` 需要处理更多父子关系。
- 活动页面关闭时，还要额外清理一组全局页面。
- 同一个子页面被多个活动复用时，生命周期容易冲突。

更合适的关系是：

```text
全局返回栈：ActivityPage

活动局部返回栈：
ActivityHomeElement
RegionElement
LevelDetailElement
MonsterElement
```

两个导航范围互不混用。

---

## 三、Prefab 应该怎样组织

推荐的活动页面结构：

```text
ActivityPage.prefab
└── ActivityPage
    ├── Header
    └── ContentRoot
        ├── ActivityHomeElement.prefab
        ├── RegionElement.prefab
        ├── LevelDetailElement.prefab
        ├── MonsterElement.prefab
        ├── ShopElement.prefab
        └── AchievementElement.prefab
```

子界面可以是嵌套 prefab，也可以直接放在父 prefab 中。

判断标准：

### 直接放进父 prefab

适合：

- 只被一个页面使用。
- 结构很小。
- 不需要单独复用。
- 需要和父页面共享布局和引用。

### 拆成子 prefab

适合：

- 结构比较复杂。
- 需要反复修改。
- 可能在多个页面复用。
- 需要独立维护视觉结构。

Prefab 是否独立，和它是否是 `UIPage` 是两件事：

```text
独立 prefab + UIElement       合法
直接嵌套 + UIElement           合法
独立 prefab + UIPage           合法
```

真正决定 `UIPage` 还是 `UIElement` 的标准，是它是否拥有独立的全局页面生命周期。

---

## 四、第一步：新增 UIElement

新增文件：

```text
Assets/Framework/Runtime/UI/UGUI/UIElement.cs
```

代码：

```csharp
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 页面内部的可切换子界面。
    /// UIElement 不由 UGUIService 加载和销毁，
    /// 而是由所属页面的 UIElementNavigator 管理显示状态。
    /// </summary>
    public class UIElement : MonoBehaviour
    {
        /// <summary>
        /// 子界面成为当前界面时调用。
        /// </summary>
        public virtual void OnEnter()
        {
        }

        /// <summary>
        /// 子界面离开当前界面时调用。
        /// </summary>
        public virtual void OnExit()
        {
        }
    }
}
```

这里使用 `OnEnter / OnExit`，避免和父页面的 `OnOpen / OnClose` 混淆：

```text
UIPage.OnOpen / OnClose
    页面创建和销毁

UIElement.OnEnter / OnExit
    页面内部显示和隐藏
```

---

## 五、第二步：新增局部导航器

新增文件：

```text
Assets/Framework/Runtime/UI/UGUI/UIElementNavigator.cs
```

第一版只管理已经存在于父 prefab 中的子界面：

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 管理一个 UIPage 内部的 UIElement。
    /// 它只负责切换和局部返回，不负责资源加载。
    /// </summary>
    public sealed class UIElementNavigator : MonoBehaviour
    {
        [SerializeField] private UIElement mInitialElement;

        private readonly Stack<UIElement> mBackStack = new Stack<UIElement>();

        public UIElement Current =>
            mBackStack.Count > 0 ? mBackStack.Peek() : null;

        public void Initialize()
        {
            if (mInitialElement == null)
            {
                return;
            }

            mBackStack.Clear();
            mBackStack.Push(mInitialElement);
            mInitialElement.gameObject.SetActive(true);
            mInitialElement.OnEnter();
        }

        public void NavigateTo(UIElement element)
        {
            if (element == null || ReferenceEquals(Current, element))
            {
                return;
            }

            if (Current != null)
            {
                Current.OnExit();
                Current.gameObject.SetActive(false);
            }

            mBackStack.Push(element);
            element.gameObject.SetActive(true);
            element.OnEnter();
        }

        public bool Back()
        {
            if (mBackStack.Count <= 1)
            {
                return false;
            }

            var current = mBackStack.Pop();
            current.OnExit();
            current.gameObject.SetActive(false);

            var previous = mBackStack.Peek();
            previous.gameObject.SetActive(true);
            previous.OnEnter();
            return true;
        }

        public void Clear()
        {
            while (mBackStack.Count > 0)
            {
                var element = mBackStack.Pop();
                element.OnExit();
                element.gameObject.SetActive(false);
            }
        }
    }
}
```

### 这段代码怎样工作

打开首页：

```text
Initialize()
Stack：[ActivityHomeElement]
```

进入地区页面：

```text
NavigateTo(RegionElement)
Stack：[ActivityHomeElement, RegionElement]
```

进入怪物介绍：

```text
NavigateTo(MonsterElement)
Stack：[ActivityHomeElement, RegionElement, MonsterElement]
```

返回：

```text
Pop MonsterElement
显示 RegionElement
```

这里的 `Stack<UIElement>` 是局部栈，和 `UGUIService` 中的 `Stack<UIPage>` 是两套不同范围的数据。

---

## 六、第三步：让 UIPage 处理局部返回

修改文件：

```text
Assets/Framework/Runtime/UI/UGUI/UIPage.cs
```

在现有 `OnClose()` 后增加：

```csharp
/// <summary>
/// 尝试处理页面内部返回。
/// 返回 true 表示页面内部已经完成返回。
/// 返回 false 表示页面内部没有可返回内容。
/// </summary>
public virtual bool HandleBack()
{
    var navigator = GetComponent<UIElementNavigator>();
    return navigator != null && navigator.Back();
}
```

没有局部导航器的页面会返回 `false`，因此仍然可以由全局 `UGUIService` 关闭。

有局部导航器的页面会优先处理内部返回。

---

## 七、第四步：修改全局 Back()

修改文件：

```text
Assets/Framework/Runtime/UI/UGUI/UGUIService.cs
```

当前 `Back()` 直接从全局栈取出页面。修改为：

```csharp
public bool Back()
{
    while (mPageBackStack.Count > 0)
    {
        var page = mPageBackStack.Peek();

        // 页面还存在时，先让页面处理自己的局部返回。
        if (page != null && mOpenedPages.ContainsKey(page))
        {
            if (page.HandleBack())
            {
                return true;
            }

            // 页面内部没有可返回内容，才关闭整个页面。
            return Close(page);
        }

        // 跳过已经失效的返回记录。
        mPageBackStack.Pop();
    }

    return false;
}
```

最终行为：

```text
UGUIService.Back()
    ↓
当前 UIPage.HandleBack()
    ├── true：只返回局部 UIElement
    └── false：关闭当前 UIPage
```

这样 `UGUIService` 不需要知道活动页面内部有多少个子界面。

---

## 八、AuthPage 示例结构

示例目录：

```text
Assets/Example/Scripts/Auth/
```

Prefab：

```text
AuthPage.prefab
└── AuthPage
    ├── LoginElement
    ├── RegisterElement
    └── ResetPasswordElement
```

子界面脚本：

```csharp
public sealed class LoginElement : UIElement
{
    [SerializeField] private RegisterElement mRegisterElement;

    public void OnRegisterClicked()
    {
        GetComponentInParent<UIElementNavigator>()
            .NavigateTo(mRegisterElement);
    }
}
```

页面脚本：

```csharp
[UIPage(
    "UI/AuthPage",
    UIOpenMode.Single,
    UILayer.Popup,
    UILifetime.Scene,
    addToBackStack: true)]
public sealed class AuthPage : UIPage, IController
{
    private UIElementNavigator mNavigator;

    public override void OnOpen()
    {
        mNavigator = GetComponent<UIElementNavigator>();
        mNavigator.Initialize();
    }

    public override void OnClose()
    {
        mNavigator.Clear();
    }

    public override bool HandleBack()
    {
        return mNavigator.Back();
    }

    IArchitecture IBelongArchitecture.GetArchitecture()
    {
        return ExampleArchitecture.Interface;
    }
}
```

登录流程：

```text
全局栈：[AuthPage]
局部栈：[LoginElement]

点击注册：
全局栈：[AuthPage]
局部栈：[LoginElement, RegisterElement]

点击返回：
全局栈：[AuthPage]
局部栈：[LoginElement]

再次点击返回：
AuthPage 被 UGUIService 关闭
```

---

## 九、哪些功能暂时不加入

第一版局部导航暂时不加入：

- 子界面异步资源加载。
- 子界面自己的 AssetHandle。
- 子界面进入全局 `UGUIService` 字典。
- 子界面单独的 Scene/Persistent 配置。
- 通用页面参数恢复系统。
- 编辑器导航图工具。

原因是当前最先需要解决的是“父页面和子界面的职责边界”。如果一开始同时加入异步加载、参数恢复和编辑器工具，模块会迅速膨胀。

---

## 十、验证顺序

用 `AuthPage` 做第一个示例：

1. 打开 `AuthPage`，默认显示 `LoginElement`。
2. 点击注册，隐藏 Login，显示 Register。
3. 点击返回，隐藏 Register，恢复 Login。
4. 再次点击返回，关闭整个 AuthPage。
5. 确认 `UGUIService` 的全局返回栈没有记录 Login 或 Register。
6. 切换场景，确认 `AuthPage` 按 `UILifetime.Scene` 清理。

再用 `ActivityPage` 验证多级流程：

```text
活动首页 → 地区 → 关卡详情 → 怪物介绍
```

验证返回顺序：

```text
怪物介绍 → 关卡详情 → 地区 → 活动首页 → 活动页面关闭
```

---

## 小结

当前框架不需要推倒重来。

保留：

```text
UGUIService
UIPage
UGUIRoot
IAssetLoader
AssetHandle
全局 Stack<UIPage>
```

新增：

```text
UIElement
UIElementNavigator
UIPage.HandleBack()
```

最终职责：

```text
UGUIService
    管理全局 UIPage 和全局返回栈

UIPage
    管理一个完整页面

UIElementNavigator
    管理页面内部的局部返回栈

UIElement
    管理具体子界面
```

这次重构的重点是增加“导航作用域”，不是重新制作一套资源加载框架。
