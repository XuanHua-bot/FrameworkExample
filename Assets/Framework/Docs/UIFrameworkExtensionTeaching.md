# UI 框架扩展教学：从页面管理到完整 UI 流程

## 文档定位

本文基于当前 `FrameworkExample` 的实际代码，讨论 UGUI 框架下一步怎样扩展。

本文中的内容分成三类：

```text
[当前] 已经存在并经过代码确认的能力
[参考] 外部框架或 Unity 官方机制提供的设计启发
[计划] 适合当前项目、但尚未写入运行时代码的扩展
```

手抄本文时，先理解职责边界，再按“一个阶段一个垂直功能”的顺序实现。

本文不把 QFramework、UnityGameFramework 或 Addressables 整套搬进项目。
只吸收已经能和当前结构配合的部分。

---

## 一、当前项目的起点

### 1. 当前已经存在的核心类型

```text
UIPage
    具体页面的 Unity 组件和页面生命周期

UGUIService
    prefab 加载、实例创建、页面登记、显示隐藏、关闭销毁、Handle 释放

UINavigationService
    页面 Push、Back、BackTo 和导航列表

UGUIRoot
    Canvas 和 Background、Normal、Popup、System、Toast 层的 Transform

IAssetLoader
    资源加载入口，隔离 Resources 或其他资源方案
```

当前文件位置：

```text
Assets/Framework/Runtime/UI/UGUI/UIPage.cs
Assets/Framework/Runtime/UI/UGUI/UGUIService.cs
Assets/Framework/Runtime/UI/UGUI/UGUIRoot.cs
Assets/Framework/Runtime/UI/Navigation/UINavigationService.cs
Assets/Framework/Runtime/Assets/IAssetLoader.cs
```

### 2. 当前页面流程

```text
请求打开
    -> UIPageAttribute 配置
    -> IAssetLoader 加载 prefab
    -> Instantiate
    -> 登记 page 和 AssetHandle
    -> OnOpen
    -> Show
    -> OnShow
    -> Hide 或 Close
    -> OnClose
    -> Destroy
    -> AssetHandle.Release
```

`CreateAsync` 只创建页面并调用 `OnOpen`，页面仍保持隐藏。
`OpenAsync` 或 `UINavigationService` 再负责显示页面。

这个顺序要保留，因为导航服务可以在 `OnOpen` 之后、`Show` 之前写入页面数据。

### 3. 当前几个概念不能混用

| 概念 | 当前含义 |
| --- | --- |
| `UIOpenMode` | 页面实例是 Single 还是 Multiple |
| `UILayer` | 页面放入哪个 Root 层 |
| `UILifetime` | 页面随场景关闭，还是切换场景后保留 |
| `BlocksUnderlyingInput` | 页面是否阻挡底层 UGUI 输入 |
| 导航列表 | 页面是否属于当前的前进/返回流程 |

例如，`Popup` 不自动等于“必须进入返回栈”，`Normal` 也不自动等于“不能阻挡输入”。

---

## 二、外部方案给当前项目的启发

### 1. UnityGameFramework 的启发

UnityGameFramework 把页面的打开、暂停、恢复、关闭等生命周期分开。

当前项目可以吸收这个思路：

```text
页面可见
页面被覆盖
页面重新成为顶部页面
页面销毁
```

但当前项目不需要直接复制完整的 UIForm、UIGroup 和动画系统。

### 2. QFramework UIKit 的启发

QFramework 将 UI 层级、打开模式、页面栈作为不同概念处理。

当前项目已经拥有其中的两部分：

```text
UIOpenMode  -> Single / Multiple
UILayer     -> Background / Normal / Popup / System / Toast
```

下一步要补齐的是：

```text
页面导航栈和 Popup 生命周期分开
```

### 3. Unity CanvasGroup 的启发

`CanvasGroup` 可以同时控制：

```text
alpha
interactable
blocksRaycasts
```

因此它适合处理页面动画期间的交互门控。

当前页面已有 Prefab 自带的 `InputBlocker`，它负责“阻挡底层输入”。
将来可以再用 `CanvasGroup` 负责“当前页面是否可交互”，两者不是一回事。

### 4. Addressables 的启发

Addressables 强调创建和释放必须成对出现。

当前项目已经用 `AssetHandle<GameObject>` 表达这条约束：

```text
一个页面实例
    -> 保存自己的 AssetHandle
    -> 关闭时释放自己的 AssetHandle
```

因此以后接入 Addressables 或 YooAsset 时，优先替换 `IAssetLoader` 实现，不修改 `UGUIService` 的页面生命周期。

---

## 三、总体扩展目标

最终希望形成下面的调用关系：

```text
业务代码
    ↓
UINavigationService 或 UIPopupService
    ↓
UGUIService
    ↓
IAssetLoader
    ↓
Prefab + UIPage
```

每一层只解决自己的问题：

```text
页面脚本：页面业务和视觉表现
导航服务：页面前进和返回
弹窗服务：短流程窗口和弹窗队列
UGUIService：实例和资源生命周期
资源加载器：资源来源和资源释放
Root：物理层级容器
```

不要创建一个同时负责导航、资源、动画、遮罩和业务事件的巨大 `UIManager`。

---

## 四、第一阶段：增加页面覆盖语义

### 1. 为什么需要覆盖语义

当前 `UINavigationService` 会隐藏上一个页面，但“被隐藏”还没有说明页面业务应该怎样处理：

```text
动画是否暂停？
输入是否关闭？
计时器是否继续？
返回后是否刷新？
```

所以需要把“页面被覆盖”单独表达出来。

### 2. 建议的枚举

计划新增：

```text
public enum UICoverMode
{
    KeepVisible,
    Hide,
    Close
}
```

含义：

| 模式 | 行为 |
| --- | --- |
| `KeepVisible` | 底层页面继续显示，适合非模态提示 |
| `Hide` | 底层页面隐藏，实例保留，适合页面导航 |
| `Close` | 底层页面关闭并释放，适合明确的一次性流程 |

第一版只需要支持 `Hide`，另外两个模式先作为设计边界保留。

### 3. 页面生命周期建议

当前已有：

```text
OnOpen()
OnShow()
OnHide()
OnClose()
```

计划增加：

```text
OnPause()
OnResume()
```

推荐语义：

```text
OnOpen：实例登记完成后调用一次
OnShow：页面变为可见时调用
OnPause：页面被上层页面覆盖时调用
OnResume：页面重新成为顶部页面时调用
OnHide：页面从可见变为隐藏时调用
OnClose：实例销毁前调用一次
```

注意：`OnPause` 不是 `OnClose`，页面仍然存在；`OnResume` 也不是重新 `OnOpen`，页面不应重复注册永久事件。

### 4. 页面导航的目标流程

```text
Push PageA
    -> PageA.OnOpen
    -> PageA.OnShow

Push PageB
    -> PageA.OnPause
    -> PageA.OnHide
    -> PageB.OnOpen
    -> PageB.OnShow

Back
    -> PageB.OnHide
    -> PageB.OnClose
    -> Destroy PageB
    -> Release PageB Handle
    -> PageA.OnShow
    -> PageA.OnResume
```

第一版实现时，`OnPause/OnResume` 可以由 `UINavigationService` 调用，`UGUIService` 仍然只管理显示、隐藏和销毁。

---

## 五、第二阶段：页面导航和弹窗导航分开

### 1. 页面栈只保存页面流程

当前 `UINavigationService` 的 `List<UIPage>` 设计继续保留。

它适合保存：

```text
MainPage
    -> InventoryPage
        -> ItemDetailPage
```

它不应该保存：

```text
ConfirmPopup
Toast
Loading
DamageText
```

### 2. Popup 的职责

未来可以新增：

```text
public interface IUIPopupService
{
    void OpenAsync<T>(
        Action<T> onOpened,
        Action<string> onFailed = null)
        where T : UIPage;

    bool Close(UIPage page);
    bool CloseTop();
}
```

Popup 服务仍然调用 `IUGUIService` 创建和关闭页面，不重复实现资源加载。

### 3. Popup 的初版规则

```text
Popup 页面：可以阻挡底层输入
Popup 页面：默认不进入页面导航列表
Popup 页面：关闭时只关闭自己
Toast 页面：不阻挡输入，不进入导航列表
System 页面：按具体页面决定是否阻挡输入
```

这套规则比根据 `UILayer` 自动猜测页面行为更可靠。

### 4. 为什么不现在立刻加入 PopupService

当前示例的真实 Popup 数量还不足以验证队列、置顶、重复打开和关闭顺序。

所以当前阶段先记录边界，等出现两个以上 Popup 流程后再抽取服务。

---

## 六、第三阶段：统一页面交互门控

### 1. 两种输入控制要区分

当前页面级 `InputBlocker` 的作用是：

```text
阻挡页面下方的 UGUI 输入
```

`CanvasGroup` 的作用是：

```text
控制当前页面是否可交互、是否接收射线
```

它们的关系是：

```text
InputBlocker：下层页面能不能收到输入
CanvasGroup：当前页面自己的控件能不能收到输入
```

### 2. 计划中的交互门控

可以在 `UIPage` 增加一个简单方法：

```text
public virtual void SetInteractable(bool interactable)
{
}
```

页面有 `CanvasGroup` 时，实现：

```text
canvasGroup.interactable = interactable;
canvasGroup.blocksRaycasts = interactable;
```

### 3. 打开和关闭动画期间的规则

```text
开始打开
    -> SetInteractable(false)
    -> 播放打开动画
    -> 页面显示完成
    -> SetInteractable(true)

开始关闭
    -> SetInteractable(false)
    -> 播放关闭动画
    -> OnClose
    -> Destroy
```

当前项目没有统一动画服务时，可以先让具体页面覆盖 `SetInteractable`，不必为了一个功能增加复杂动画抽象。

---

## 七、第四阶段：资源加载继续保持中立

### 1. 当前正确方向

`UGUIService` 依赖：

```text
IAssetLoader
```

它不应该直接依赖：

```text
Resources
Addressables
YooAsset
HybridCLR
```

### 2. 将来的适配方式

```text
ResourcesAssetLoader
    -> 适合当前示例

AddressablesAssetLoader
    -> 以后接入 Addressables

YooAssetLoader
    -> 以后接入 YooAsset

MockAssetLoader
    -> 单元测试或编辑器测试
```

所有实现都遵守同一条规则：

```text
LoadAsync 成功
    -> 返回页面使用的 Handle

页面关闭
    -> 销毁实例
    -> Release 对应 Handle
```

不要在 `UGUIService` 中同时保留多套第三方资源释放分支。

---

## 八、第五阶段：页面参数继续使用强类型

当前项目已有：

```text
IUIData
IUIPageDataReceiver<TData>
```

继续使用这种方式：

```text
public sealed class DetailPageData : IUIData
{
    public int ItemId;
}

public sealed class DetailPage
    : UIPage, IUIPageDataReceiver<DetailPageData>
{
    public void SetData(DetailPageData data)
    {
        // 保存本次页面数据，OnShow 时刷新显示。
    }
}
```

调用方：

```text
navigation.PushAsync<DetailPage, DetailPageData>(
    new DetailPageData { ItemId = 1001 },
    page => { },
    reason => { });
```

不要退回到：

```text
Open<T>(object data)
```

强类型参数让页面和调用方在编译期保持一致，也符合当前项目的教学目标。

---

## 九、不要过早加入的设计

以下方案暂缓：

### 1. 不立即创建完整 UIManager

现有 `UGUIService` 已经是页面资源和实例管理者。
再创建一个同样负责资源、实例和生命周期的 `UIManager` 会造成职责重复。

### 2. 不立即统一 UGUI 和 UI Toolkit

两者的实例、布局、事件和资源模型不同。
当前没有真实 UI Toolkit 使用场景，先保持实现边界。

### 3. 不给所有页面加对象池

普通页面关闭时需要完整清理事件、数据和资源。
对象池优先留给 Toast、FloatingText、ListItem 等高频短生命周期对象。

### 4. 不用 Layer 推断输入遮挡

`UILayer.Popup` 不等于必然模态，`UILayer.Normal` 也可能需要阻挡底层输入。
当前页面使用 `BlocksUnderlyingInput` 单独表达这个意图。

### 5. 不为了扩展而扩展 Root 层

当前五个层已经可以支撑示例项目。
只有在出现明确的绘制顺序或输入隔离需求时再增加层。

---

## 十、推荐的手抄实施顺序

### 第 1 课：确认当前基线

阅读：

```text
UIPage.cs
UGUIService.cs
UINavigationService.cs
UGUIRoot.cs
IAssetLoader.cs
```

手写一张关系图：

```text
业务调用 -> 导航服务 -> UGUIService -> IAssetLoader -> Prefab
```

验收：能够说出每个类负责什么，以及哪个类保存页面和 Handle 的关系。

### 第 2 课：设计覆盖策略

先写：

```text
UICoverMode
OnPause
OnResume
```

再决定 `UINavigationService` 在 Push 和 Back 的哪个位置调用它们。

验收：能够画出 PageA 被 PageB 覆盖和恢复的时序图。

### 第 3 课：拆分页面栈和 Popup

先在文档和示例中规定：

```text
Normal 页面进入导航列表
Popup、System、Toast 不自动进入导航列表
```

出现真实 Popup 流程后，再创建 `IUIPopupService`。

验收：点击 Back 时不会误关闭 Toast 或确认弹窗。

### 第 4 课：加入交互门控

给一个示例页面增加 `CanvasGroup`，验证：

```text
页面关闭时不能重复点击
页面动画期间不能重复提交
InputBlocker 仍然能阻挡底层页面
```

验收：分别说明 `InputBlocker` 和 `CanvasGroup` 解决的不同问题。

### 第 5 课：验证资源释放

为 Single、Multiple、Scene、Persistent 各记录一次：

```text
加载次数
创建实例数
关闭次数
Destroy 次数
Handle.Release 次数
```

验收：每一个已关闭实例都有且只有一次资源释放。

---

## 十一、最终目标结构

```text
Framework
└── UI
    ├── UGUI
    │   ├── UIPage
    │   ├── UIPageAttribute
    │   ├── UIPageOptions
    │   ├── UGUIRoot
    │   └── UGUIService
    ├── Navigation
    │   ├── UINavigationService
    │   └── PageHistory
    ├── Popup
    │   └── UIPopupService
    ├── Interaction
    │   └── UIInteractionGate
    └── Diagnostics
        └── UIDebugWindow
```

最终数据流：

```text
页面请求
    -> 导航或 Popup 服务
    -> UGUIService 创建页面
    -> 页面 OnOpen
    -> 写入强类型数据
    -> Show
    -> OnShow / OnPause / OnResume
    -> Close
    -> OnClose
    -> Destroy
    -> Handle.Release
```

## 总结

当前项目下一步的重点不是增加更多类，而是把已有行为的边界说清楚：

```text
UGUIService 管资源和实例
UINavigationService 管页面历史
Popup 服务管弹窗流程
UIPage 管页面表现
UILayer 管显示位置
InputBlocker 管底层输入遮挡
CanvasGroup 管当前页面交互
IAssetLoader 管资源来源
```

按照这个边界扩展，后续接入动画、Addressables、YooAsset、对象池或调试工具时，都可以只替换对应模块，不必推翻现有页面代码。

---

## 十二、第一条可直接实现的垂直功能

下面开始写具体代码。本节的目标是完成：

```text
PageA 被 PageB 覆盖时暂停
PageB 返回 PageA 时恢复
页面有 CanvasGroup 时统一开关交互
```

本节只修改三个运行时代码文件：

```text
Assets/Framework/Runtime/UI/UGUI/UIPage.cs
Assets/Framework/Runtime/UI/UGUI/UIPageOptions.cs
Assets/Framework/Runtime/UI/Navigation/UINavigationService.cs
```

### 12.1 修改 UIPage.cs

在 `UIPage` 类中保留已有的 `OnOpen`、`OnShow`、`OnHide`、`OnClose`，并增加下面四部分。

#### 第一步：让页面根物体自动拥有 CanvasGroup

在 `UIPage` 类声明上增加：

```csharp
[RequireComponent(typeof(CanvasGroup))]
public class UIPage : MonoBehaviour
```

`RequireComponent` 的作用是：当你把 `UIPage` 或具体页面 Controller 添加到 GameObject 时，Unity 自动补上 `CanvasGroup`。

#### 第二步：增加 CanvasGroup 字段和自动引用

放在现有 `mInputBlocker` 字段后面：

```text
[Header("Interaction")]
[SerializeField]
private CanvasGroup mCanvasGroup;
```

在 `UIPage` 中增加：

```csharp
protected virtual void Awake()
{
    if (mCanvasGroup == null)
    {
        mCanvasGroup = GetComponent<CanvasGroup>();
    }

    if (mCanvasGroup == null)
    {
        mCanvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
}

protected virtual void Reset()
{
    mCanvasGroup = GetComponent<CanvasGroup>();
}
```

`RequireComponent` 负责常规 Inspector 配置，`Awake` 是旧 Prefab 或运行时缺失时的兜底，`Reset` 用于在 Inspector 添加组件或执行 Reset 时自动填充引用。

#### 第三步：增加交互控制方法

放在 `SetInputBlocker` 方法后面：

```text
/// <summary>
/// 控制当前页面自己的控件是否可以交互。
/// 它不负责阻挡页面下方的输入；下方输入由 InputBlocker 负责。
/// </summary>
public virtual void SetInteractable(bool interactable)
{
    if (mCanvasGroup == null)
    {
        return;
    }

    mCanvasGroup.interactable = interactable;
    mCanvasGroup.blocksRaycasts = interactable;
}
```

#### 第四步：增加暂停和恢复生命周期

放在 `OnHide` 和 `OnClose` 之间：

```text
/// <summary>
/// 页面被上层页面覆盖时调用。实例仍然存在。
/// </summary>
public virtual void OnPause() { }

/// <summary>
/// 页面重新成为顶部页面时调用。
/// </summary>
public virtual void OnResume() { }
```

#### 第五步：UIPage.cs 修改后的核心结构

手抄完成后，核心方法顺序应当接近下面这样：

```text
public virtual void OnOpen() { }
public virtual void OnShow() { }
public virtual void OnPause() { }
public virtual void OnResume() { }
public virtual void OnHide() { }
public virtual void OnClose() { }
```

不要在 `OnPause` 中销毁页面，也不要在 `OnResume` 中重复注册长期事件。

### 12.2 修改 UIPageOptions.cs

在 `UIOpenMode` 后面增加：

```text
/// <summary>
/// 页面被其他页面覆盖时的处理方式。
/// 第一版导航只使用 Hide，其他值先作为扩展边界保留。
/// </summary>
public enum UICoverMode
{
    KeepVisible,
    Hide,
    Close
}
```

当前先不把 `UICoverMode` 加入 `UIPageAttribute` 构造函数。
原因是当前导航服务已经固定使用“隐藏旧页面、保留实例”的流程，先完成生命周期调用，再决定配置是否需要公开。

### 12.3 修改 UINavigationService.cs

#### 第一步：Push 新页面时暂停旧页面

在 `PushInternal` 中找到当前代码：

```text
if (mPages.Count>0)
{   
    mUGUIService.Hide(mPages[mPages.Count - 1]);
}
```

替换为：

```text
if (mPages.Count > 0)
{
    var previousPage = mPages[mPages.Count - 1];
    previousPage.OnPause();
    previousPage.SetInteractable(false);
    mUGUIService.Hide(previousPage);
}
```

然后保留下面的顺序：

```text
applyData?.Invoke(page);
mPages.Add(page);
mUGUIService.Show(page);
page.SetInteractable(true);
page.OnResume();
onOpened?.Invoke(page);
```

这里 `OnResume` 对新页面第一次打开也会调用一次。
如果希望第一次打开不调用 `OnResume`，可以把它移动到 `Back()` 中；当前教学版本统一把“成为顶部页面”定义为恢复动作，后续页面可以自行区分首次初始化和返回刷新。

#### 第二步：Back 时恢复前一个页面

找到当前代码：

```text
CloseTopPage();
mUGUIService.Show(mPages[mPages.Count - 1]);
return true;
```

替换为：

```text
CloseTopPage();

var previousPage = mPages[mPages.Count - 1];
mUGUIService.Show(previousPage);
previousPage.SetInteractable(true);
previousPage.OnResume();
return true;
```

#### 第三步：BackTo 时恢复目标页面

找到当前代码结尾：

```text
mUGUIService.Show(mPages[targetIndex]);
return true;
```

替换为：

```text
var targetPage = mPages[targetIndex];
mUGUIService.Show(targetPage);
targetPage.SetInteractable(true);
targetPage.OnResume();
return true;
```

#### 第四步：关闭顶部页面前关闭交互

将 `CloseTopPage` 改成：

```text
private void CloseTopPage()
{
    var lastIndex = mPages.Count - 1;
    var page = mPages[lastIndex];

    // 先从导航列表移除，避免 Close 触发 pageClosed 时重复移除。
    mPages.RemoveAt(lastIndex);
    page.SetInteractable(false);
    mUGUIService.Close(page);
}
```

### 12.4 页面脚本如何使用新生命周期

示例页面：

```text
public sealed class ExampleScenePage : UIPage
{
    public override void OnOpen()
    {
        // 只做一次的初始化：查找组件、绑定按钮、注册事件。
    }

    public override void OnPause()
    {
        // 停止只在顶部页面运行的动画或刷新。
    }

    public override void OnResume()
    {
        // 从详情页返回时刷新一次显示内容。
    }

    public override void OnClose()
    {
        // 注销 OnOpen 中注册的事件。
    }
}
```

不要在 `OnResume` 中重复 `AddListener`，否则每返回一次就会多绑定一次按钮。

### 12.5 Prefab 配置

推荐把具体页面 Controller 挂在页面 Prefab 的根物体上：

```text
PageRoot
├── ExampleScenePage (继承 UIPage 的 Controller)
├── CanvasGroup (由 RequireComponent 自动添加)
├── InputBlocker
└── VisualContent
```

不需要再手动把 `CanvasGroup` 拖到 `mCanvasGroup`；`Awake` 会从 Controller 所在的根物体自动取得它。

页面原有的 `InputBlocker` 仍然单独绑定到 `mInputBlocker`。

### 12.5.1 CanvasGroup 可以挂在哪里

`CanvasGroup` **不要求挂在 Canvas 上**，它可以挂在任意拥有 `GameObject` 的物体上。

它会影响“自己以及所有子物体”的 UI Graphic：

```text
PageRoot 上的 CanvasGroup
    -> 影响整个页面

Panel 上的 CanvasGroup
    -> 只影响这个 Panel 和它的子物体

Button 上的 CanvasGroup
    -> 只影响这个 Button 及其子物体
```

所以当前框架应该把它放在继承 `UIPage` 的页面根物体上。这样 `SetInteractable(false)` 才能一次控制整个页面。

如果某个子物体需要忽略父级 CanvasGroup，可以在它自己的 `CanvasGroup` 上使用：

```csharp
childCanvasGroup.ignoreParentGroups = true;
```

这个选项应当只在确实需要独立交互的局部 UI 中使用。

两者分别负责：

```text
CanvasGroup.blocksRaycasts
    当前页面自己的控件是否接收输入

InputBlocker 的 Graphic Raycast Target
    页面下方的页面是否接收输入
```

### 12.6 第一条功能的验证流程

使用已有的导航示例：

```text
1. 打开 ExampleScenePage。
2. 打开 ExampleDetailPage。
3. 观察 A 页面调用 OnPause，随后隐藏。
4. 观察 B 页面显示并可交互。
5. 点击返回。
6. 观察 B 页面关闭，A 页面调用 OnResume。
7. 连续点击返回按钮，确认不会重复关闭页面。
```

编译验证：

```text
dotnet build E:\unityXiangmu\FrameworkExample\Assembly-CSharp.csproj --no-restore
```

验收条件：

```text
0 个编译错误。
PageA 的 OnPause 只在被覆盖时调用。
PageA 的 OnResume 在返回时调用。
PageB 关闭后仍然只释放一次 Handle。
页面没有 CanvasGroup 时，SetInteractable 不报错。
```

---

## 十三、第二条可直接实现的垂直功能：最小 Popup 服务

第一条功能验证通过后，再手抄下面这条。它只复用 `IUGUIService`，不复制资源加载代码。

### 13.1 新建 IUIPopupService.cs

文件：

```text
Assets/Framework/Runtime/UI/Popup/IUIPopupService.cs
```

代码：

```text
using System;

namespace GameFramework
{
    public interface IUIPopupService : IUtility
    {
        void OpenAsync<T>(
            Action<T> onOpened,
            Action<string> onFailed = null)
            where T : UIPage;

        bool Close(UIPage page);
        bool CloseTop();
    }
}
```

### 13.2 新建 UIPopupService.cs

文件：

```text
Assets/Framework/Runtime/UI/Popup/UIPopupService.cs
```

代码：

```text
using System;
using System.Collections.Generic;

namespace GameFramework
{
    public sealed class UIPopupService : IUIPopupService, IDeinit
    {
        private readonly IUGUIService mUGUIService;
        private readonly List<UIPage> mPopups = new List<UIPage>();

        public UIPopupService(IUGUIService uguiService)
        {
            mUGUIService = uguiService
                ?? throw new ArgumentNullException(nameof(uguiService));
            mUGUIService.pageClosed += OnPageClosed;
        }

        public void OpenAsync<T>(
            Action<T> onOpened,
            Action<string> onFailed = null)
            where T : UIPage
        {
            mUGUIService.OpenAsync<T>(page =>
            {
                mPopups.Add(page);
                onOpened?.Invoke(page);
            }, onFailed);
        }

        public bool Close(UIPage page)
        {
            if (page == null || !mPopups.Remove(page))
            {
                return false;
            }

            return mUGUIService.Close(page);
        }

        public bool CloseTop()
        {
            if (mPopups.Count == 0)
            {
                return false;
            }

            return Close(mPopups[mPopups.Count - 1]);
        }

        public void Deinit()
        {
            mUGUIService.pageClosed -= OnPageClosed;

            var pages = new List<UIPage>(mPopups);
            mPopups.Clear();

            foreach (var page in pages)
            {
                mUGUIService.Close(page);
            }
        }

        private void OnPageClosed(UIPage page)
        {
            mPopups.Remove(page);
        }
    }
}
```

### 13.3 在 Architecture 中注册

在注册 `IUGUIService` 后增加：

```text
RegisterUtility<IUIPopupService>(
    new UIPopupService(GetUtility<IUGUIService>()));
```

依赖顺序必须是：

```text
IAssetLoader
    -> IUGUIService
    -> IUIPopupService
```

### 13.4 Popup 的调用方式

```text
private void OpenConfirmPopup()
{
    this.GetUtility<IUIPopupService>().OpenAsync<ConfirmPopup>(
        popup =>
        {
            // Popup 已经创建并显示。
        },
        reason => Debug.LogError(reason));
}
```

Popup 关闭按钮中调用：

```text
private void CloseSelf()
{
    this.GetUtility<IUIPopupService>().Close(this);
}
```

### 13.5 Popup 的限制

这个最小版本有意不做以下事情：

```text
不进入 UINavigationService 的页面历史
不自动管理 Popup 队列
不自动播放动画
不根据 UILayer 推断输入遮挡
不替代 UGUIService 的资源释放
```

先验证“Popup 不会影响页面 Back”这一条边界，再考虑队列、优先级和动画。

## 14. 对象池契约增强：先手抄，再决定是否加入

当前项目已经有两套对象池：

    ObjectPool<T>：普通 C# 引用对象
    GameObjectPool：Unity GameObject

本节只讨论 `ObjectPool<T>` 的一个边界问题，不要求现在修改项目源码。

当前 `Release(T item)` 只检查对象是否已经在空闲集合中，没有检查对象是否由当前对象池创建。这样一个外部对象也可能被错误归还到池中，造成 `CountAll` 和实际对象数量不一致。

文件：

    Assets/Framework/Runtime/Pooling/ObjectPool.cs

### 14.1 增加对象归属集合

在 `mInactiveSet` 后增加：

    private readonly HashSet<T> mKnownItems = new HashSet<T>();

它记录当前对象池创建过的全部对象，包括活动对象和空闲对象。

### 14.2 在创建对象时登记

将 `CreateItem()` 中的代码：

    mCountAll++;
    return item;

改为：

    mCountAll++;
    mKnownItems.Add(item);
    return item;

### 14.3 在归还对象时检查归属

在 `Release(T item)` 的空对象检查后加入：

    if (!mKnownItems.Contains(item))
    {
        throw new InvalidOperationException("The item was not created by this pool.");
    }

然后再执行原来的重复归还检查：

    if (!mInactiveSet.Add(item))
    {
        throw new InvalidOperationException("The same item was released to this pool twice.");
    }

### 14.4 清理空闲对象时移除归属

在 `ClearInactive()` 中，取出对象后加入：

    mKnownItems.Remove(item);

完整顺序应当是：

    var item = mInactiveItems.Pop();
    mInactiveSet.Remove(item);
    mKnownItems.Remove(item);
    mOnDestroy?.Invoke(item);
    mCountAll--;

### 14.5 这一改动解决什么问题

    pool.Release(不是本池创建的对象)
    -> 立即抛出异常

    pool.Release(同一个对象两次)
    -> 仍然抛出重复归还异常

    pool.ClearInactive()
    -> 空闲对象从对象池归属记录中移除

这不是新增对象池功能，而是补充对象池的所有权约束。手抄完成后，再单独验证 CountAll、CountActive 和 CountInactive 的变化。
