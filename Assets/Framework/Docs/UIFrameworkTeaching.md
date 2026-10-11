# 当前 UGUI 框架教学文档

## 课程目标

这篇文档不从某一个方法开始讲，而是先建立整个 UI 框架的结构，再解释每个类为什么存在。

当前框架的核心目标是：

> 业务代码只需要请求“打开哪个页面”，UI 框架负责加载 prefab、创建实例、选择层级、管理生命周期和释放资源。

当前讨论的代码位于：

```text
E:/unityXiangmu/FrameworkExample/Assets/Framework/Runtime/UI/UGUI/
```

示例代码位于：

```text
E:/unityXiangmu/FrameworkExample/Assets/Example/
```

---

## 一、先看整体结构

当前 UI 系统可以简化成三层：

```text
具体页面
    ↓
UGUIService
    ↓
UGUIRoot
```

再加上两个外部依赖：

```text
UGUIService
    ├── IAssetLoader       负责加载 prefab
    └── Architecture       负责提供 UI Service
```

### 1. 具体页面

例如：

```text
ExampleScenePage.cs
ExampleScenePage.prefab
```

页面脚本负责页面自己的交互，prefab 负责页面的视觉结构。

### 2. UGUIService

文件：

```text
Assets/Framework/Runtime/UI/UGUI/UGUIService.cs
```

它是当前 UI 模块的核心管理者，负责：

- 读取页面配置。
- 请求资源加载。
- 创建页面实例。
- 把实例放到正确的 UI 层。
- 保存页面实例和资源 Handle 的关系。
- 调用 `OnOpen()` 和 `OnClose()`。
- 处理 Single、Multiple、Scene、Persistent。
- 处理返回栈。

### 3. UGUIRoot

文件：

```text
Assets/Framework/Runtime/UI/UGUI/UGUIRoot.cs
```

它只负责保存 UI 的物理层级：

```text
UGUIRoot
├── BackgroundLayer
├── NormalLayer
├── PopupLayer
├── SystemLayer
└── ToastLayer
```

`UGUIRoot` 不负责加载页面，也不负责关闭页面。它只是页面实例的父节点提供者。

---

## 二、一个页面由什么组成

一个完整页面通常由四部分组成：

```text
页面脚本
页面 prefab
UIPageAttribute
UGUIService
```

### 1. 页面 prefab：负责“长什么样”

例如页面 prefab 内部可能是：

```text
SettingsPage
├── Background
├── Title
├── VolumeSlider
├── CloseButton
└── Message
```

Prefab 保存：

- `Image`
- `Text`
- `Button`
- `Slider`
- `CanvasGroup`
- 动画组件
- RectTransform 布局

Prefab 可以理解为页面的“身体”。

### 2. 页面脚本：负责“怎么工作”

页面脚本继承 `UIPage`：

```csharp
public sealed class SettingsPage : UIPage, IController
{
    public override void OnOpen()
    {
        // 初始化显示内容
        // 注册按钮事件
    }

    public override void OnClose()
    {
        // 注销按钮事件
        // 注销外部事件
    }
}
```

页面脚本不负责：

- 加载自己的 prefab。
- 决定自己放在哪个 Layer。
- 保存自己的 Handle。
- 直接操作 `UGUIService` 的字典。

### 3. UIPageAttribute：负责“页面配置”

示例：

```csharp
[UIPage(
    "UI/ExampleScenePage",
    UIOpenMode.Single,
    UILayer.Popup,
    UILifetime.Scene,
    addToBackStack: true)]
```

它配置了五件事：

| 配置 | 含义 |
| --- | --- |
| `UI/ExampleScenePage` | prefab 的资源路径 |
| `Single` | 是否只允许一个实例 |
| `Popup` | 放入哪个 UI 层 |
| `Scene` | 是否随场景切换销毁 |
| `true` | 是否加入返回栈 |

### 4. UGUIService：负责“页面管理”

页面脚本只描述具体页面，`UGUIService` 管理所有页面共有的流程。

这就是为什么业务代码可以这样写：

```csharp
this.GetUtility<IUGUIService>().OpenAsync<ExampleScenePage>(
    page => Debug.Log("打开成功"),
    reason => Debug.LogError(reason));
```

业务代码不需要自己写：

```csharp
Resources.Load(...)
Instantiate(...)
SetParent(...)
Destroy(...)
```

---

## 三、Architecture、Bootstrap 和 UGUIService 的关系

### 1. Architecture 保存服务

示例文件：

```text
Assets/Example/Scripts/ExampleArchitecture.cs
```

Architecture 注册：

```csharp
RegisterUtility<IUGUIService>(new UGUIService(assetLoader));
```

之后业务代码可以通过：

```csharp
this.GetUtility<IUGUIService>()
```

取得 UI 服务。

### 2. UGUIRootBootstrap 绑定场景 Root

Architecture 中的 `UGUIService` 是普通 C# 对象，不是场景组件。

场景中的 `UGUIRoot` 是 Unity 的 GameObject。

所以需要 Bootstrap 做连接：

```text
场景中的 UGUIRoot
        ↓
UGUIRootBootstrap
        ↓
ExampleArchitecture
        ↓
UGUIService.BindRoot(root)
```

Bootstrap 的职责只有一个：

> 把当前场景中的 UGUIRoot 交给当前项目的 UGUIService。

它不负责加载页面，也不负责页面导航。

---

## 四、四个核心配置维度

页面配置中有四个容易混淆的概念：

```text
OpenMode     创建数量
Layer        视觉层级
Lifetime     存活时间
BackStack    是否属于导航历史
```

它们是不同维度，不能互相替代。

### 1. UIOpenMode：创建几个

#### Single

```text
同一个页面类型最多存在一个实例
```

适合：

- 设置页
- 背包页
- 暂停页
- 任务页

#### Multiple

```text
每次打开都创建新实例
```

适合：

- 伤害跳字
- 多个奖励提示
- 多个 Tooltip
- 多个飘窗

Multiple 页面可以出现：

```text
instance1
instance2
instance3
```

它们使用同一个 prefab，但每个实例都是独立对象。

### 2. UILayer：放在哪里

```text
Background：背景 UI
Normal：HUD、普通页面
Popup：弹窗、暂停窗口
System：Loading、系统提示
Toast：短时提示
```

Layer 决定页面的父节点，不决定页面是否唯一，也不决定页面是否跨场景。

### 3. UILifetime：活多久

#### Scene

```text
切换场景时关闭页面
```

#### Persistent

```text
切换场景后保留页面
```

Lifetime 决定页面的生命周期，不决定页面是不是 Single。

例如下面两种都是合法组合：

```text
Single + Scene
Single + Persistent
```

### 4. AddToBackStack：是否属于返回历史

只有真正代表用户导航的页面才写：

```csharp
addToBackStack: true
```

框架不会再根据 Layer 猜测，因为同一个 `NormalLayer` 中可能同时有：

- 不应该被返回关闭的 HUD。
- 应该被返回关闭的背包页。

返回资格是页面自己的意图，所以由页面显式声明。

---

## 五、打开 Single 页面时的完整流程

业务调用：

```csharp
uiService.OpenAsync<ExampleScenePage>(onOpened, onFailed);
```

### 第一步：取得页面类型

```csharp
var pageType = typeof(ExampleScenePage);
```

### 第二步：读取页面特性

```csharp
var attribute = GetPageAttribute(pageType);
```

得到：

```text
资源路径
打开模式
显示层
生命周期
返回栈配置
```

### 第三步：查询已打开的 Single 页面

```csharp
mOpenedSinglePages.TryGetValue(pageType, out var openedPage)
```

如果已经打开：

```text
直接返回原实例
不重新加载
不重新创建
```

### 第四步：查询正在加载的 Single 页面

```csharp
mOpeningSinglePages.TryGetValue(pageType, out var pending)
```

如果正在加载：

```text
不重新发起加载
把本次回调加入 PendingOpen
等待第一次加载完成
```

### 第五步：根据 Layer 找父节点

例如：

```csharp
attribute.Layer == UILayer.Popup
```

最终找到：

```text
UGUIRoot.PopupLayer
```

### 第六步：调用资源加载器

```csharp
mAssetLoader.LoadAsync<GameObject>(
    attribute.ResourceKey,
    handle => CompleteSingleOpen(...),
    reason => FailSingleOpen(...));
```

`UGUIService` 不直接依赖 `Resources.Load`，它只依赖 `IAssetLoader`。

### 第七步：资源完成后创建实例

```csharp
var instance = Object.Instantiate(handle.Asset, parent);
var page = instance.GetComponent<T>();
```

这里的 `page` 是场景中刚创建的具体页面组件实例。

### 第八步：登记页面

```csharp
mOpenedPages.Add(
    page,
    new OpenedPageEntry(handle, lifetime));

mOpenedSinglePages.Add(pageType, page);
```

### 第九步：加入返回栈

如果页面配置了：

```csharp
addToBackStack: true
```

则：

```csharp
mPageBackStack.Push(page);
```

### 第十步：调用页面生命周期

```csharp
page.OnOpen();
pending.NotifyOpened(page);
```

页面到这里才算正式打开完成。

---

## 六、打开 Multiple 页面有什么不同

Multiple 页面不会查询：

```csharp
mOpenedSinglePages
mOpeningSinglePages
```

因为它不需要合并重复请求。

每次调用都会：

```text
加载 prefab
创建新实例
保存到 mOpenedPages
调用 OnOpen
返回新实例
```

Multiple 页面也不会自动进入返回栈。

示例：

```csharp
[UIPage(
    "UI/ExampleMultiplePage",
    UIOpenMode.Multiple,
    UILayer.Toast,
    UILifetime.Scene)]
```

关闭 Multiple 页面时必须传入具体实例：

```csharp
this.GetUtility<IUGUIService>().Close(this);
```

这样只会关闭当前实例。

---

## 七、UGUIService 中几份数据的作用

### mOpenedPages

类型：

```csharp
Dictionary<UIPage, OpenedPageEntry>
```

保存所有已经创建的页面：

```text
页面实例 → Handle + Lifetime
```

Multiple 页面也在这里。

### mOpenedSinglePages

类型：

```csharp
Dictionary<Type, UIPage>
```

保存：

```text
页面类型 → Single 页面实例
```

它用于：

- 判断 Single 页面是否已经打开。
- `Close<T>()` 根据类型找到页面。

### mOpeningSinglePages

类型：

```csharp
Dictionary<Type, PendingOpen>
```

保存正在加载的 Single 请求，用于防止同一个页面重复加载。

### mPageAttributes

类型：

```csharp
Dictionary<Type, UIPageAttribute>
```

缓存页面特性，避免反复读取反射配置。

### mPageBackStack

类型：

```csharp
Stack<UIPage>
```

只保存加入导航历史的页面。

```text
Push(PageA)
Push(PageB)
Pop() -> PageB
```

---

## 八、关闭页面的完整流程

### 1. 关闭具体实例

```csharp
uiService.Close(page);
```

流程：

```text
根据 page 找到 OpenedPageEntry
        ↓
从 mOpenedPages 移除
        ↓
从返回栈清理 page
        ↓
从 mOpenedSinglePages 清理 Single 记录
        ↓
调用 page.OnClose()
        ↓
Destroy(page.gameObject)
        ↓
handle.Release()
```

### 2. 按类型关闭 Single 页面

```csharp
uiService.Close<ExampleScenePage>();
```

流程：

```text
根据类型找到 mOpenedSinglePages
        ↓
得到具体 page 实例
        ↓
调用 Close(page)
```

所以真正负责销毁和释放资源的入口仍然只有：

```csharp
Close(UIPage page)
```

### 3. 返回

```csharp
uiService.Back();
```

流程：

```text
从 Stack.Pop 取出最后打开的页面
        ↓
调用 Close(page)
        ↓
OnClose
        ↓
Destroy
        ↓
Release Handle
```

当前返回栈是“关闭顺序栈”，还不是完整的页面导航系统。它不会自动保存页面参数、隐藏上一页或恢复页面状态。

---

## 九、场景切换时发生什么

`UGUIService` 监听：

```csharp
SceneManager.activeSceneChanged
```

场景变化后：

```text
mSceneVersion 增加
        ↓
查找所有 UILifetime.Scene 页面
        ↓
调用 Close(page)
        ↓
取消仍在加载的 Scene 页面
        ↓
清空返回栈
```

Persistent 页面不在场景页面清理列表中，所以会继续保留。

---

## 十、UIPage.OnOpen 和 Unity Start 的区别

Unity 自己的生命周期：

```text
Awake
OnEnable
Start
```

框架自己的页面生命周期：

```text
UGUIService 创建并登记页面
        ↓
UIPage.OnOpen()
        ↓
页面正式可用
```

推荐把页面打开时的业务初始化写在：

```csharp
OnOpen()
```

把关闭时的清理写在：

```csharp
OnClose()
```

当前示例中的 `ExampleScenePage` 和 `ExamplePersistentPage` 仍使用了 Unity `Start()` 绑定按钮，`ExampleMultiplePage` 使用了 `OnOpen()`。这属于示例代码风格尚未完全统一，框架本身推荐统一使用 `OnOpen/OnClose` 管理页面业务生命周期。

---

## 十一、Prefab、页面脚本和框架的边界

### Prefab 负责

- UI 层级结构。
- 图片、文本、按钮等组件。
- 默认尺寸、锚点和样式。
- 动画组件和 CanvasGroup。

### UIPage 脚本负责

- 初始化页面数据。
- 注册和注销按钮事件。
- 响应页面交互。
- 关闭当前页面。

### UGUIService 负责

- 页面资源加载。
- 页面实例创建。
- UI 层级选择。
- 生命周期管理。
- 返回栈管理。
- Handle 释放。

因此页面脚本不应该直接操作：

```csharp
mOpenedPages
mPageBackStack
```

这些属于服务内部状态。

---

## 十二、当前示例如何验证

示例控制器：

```text
Assets/Example/Scripts/ExampleControlPanel.cs
```

运行场景后可以验证：

### 返回栈

1. 点击“打开导航页面 A”。
2. 点击“打开导航页面 B”。
3. 点击“返回上一页”。
4. B 应该先关闭。
5. 再次点击返回，A 才会关闭。

### Persistent 页面

1. 点击“打开 Persistent 页面”。
2. 点击“切换场景”。
3. Persistent 页面继续存在。

### Multiple 页面

1. 连续点击“打开 Multiple 页面”。
2. 屏幕上出现多个实例。
3. 点击某个实例自己的关闭按钮。
4. 只关闭当前实例。

### Scene 页面

切换场景后，Scene 页面会被 `UGUIService` 自动关闭。

---

## 十三、当前已经完成和还没有完成的功能

### 已完成

- UGUI 页面 prefab 加载。
- Single 页面。
- Multiple 页面。
- Scene 生命周期。
- Persistent 生命周期。
- UGUIRoot 和 Layer。
- PendingOpen。
- 页面实例和 Handle 管理。
- 基础返回栈。
- 独立 FrameworkExample 示例项目。

### 尚未完成

- UIToolkit Service。
- UGUI 和 UIToolkit 的统一页面抽象。
- 页面动画管理器。
- 通用遮罩和输入阻挡。
- 页面参数传递。
- 页面状态恢复。
- 高频 Multiple UI 对象池。
- 编辑器页面配置工具。

---

## 结课总结

当前框架最核心的三部分是：

```text
UIPage：具体页面
UGUIService：页面管理者
UGUIRoot：页面容器
```

`UIPageAttribute` 告诉服务页面应该怎样打开；`IAssetLoader` 提供 prefab；`UGUIService` 创建并管理页面；`UGUIRoot` 提供显示层；`UIPage.OnOpen/OnClose` 负责页面自身的业务生命周期。

返回栈只是 `UGUIService` 的附加导航能力，不负责替代页面实例表，也不负责替代完整导航系统。

当前 UI 模块可以概括为：

> **一个基于 Architecture、资源加载器和 UGUIRoot 的页面生命周期管理模块。**
