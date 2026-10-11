# UGUI 返回栈与页面预制体

## 当前模块进度（1/2）

当前 UGUI 已经支持：

- `UIPageAttribute` 配置资源路径、打开模式、显示层和生命周期。
- `Single` 页面和 `Multiple` 页面。
- `Scene` 页面和 `Persistent` 页面。
- `UGUIRoot` 统一提供 Canvas 和各 UI 层级。
- `UGUIService` 管理页面实例、资源 Handle、生命周期回调和销毁。

本阶段新增两个设计：

1. 返回栈：记录可返回页面的打开顺序。
2. 页面预制体组织规则：明确页面脚本、页面 prefab 和通用 UI 组件的职责。

本阶段先实现 UGUI 返回栈，Prefab 组织部分只确定规则，不新增通用组件库。

> **最终设计决定：返回栈使用 `Stack<UIPage>`。**
>
> 本文不使用 `List<UIPage>` 作为返回栈实现。打开页面使用 `Push`，返回使用 `Pop`；页面被业务主动关闭时，通过 `RemoveFromBackStack` 清理指定实例。页面是否进入返回栈，由 `UIPageAttribute` 的 `addToBackStack` 显式声明。

## 一、返回栈解决什么问题

当前页面关闭需要调用方自己保存页面实例：

```csharp
uiService.Close(currentPage);
```

当页面数量增加后，业务代码还需要自己记录“当前页面的上一页”。返回栈负责保存这个顺序：

```text
打开 PageA
[PageA]

打开 PageB
[PageA, PageB]

返回
关闭 PageB
[PageA]
```

返回栈只记录导航顺序，不负责替代页面登记表。它的实现固定为 `Stack<UIPage>`，不能改成 `List<UIPage>` 后再混用 `Add`。

当前 UGUIService 中的几份数据分别负责不同工作：

| 数据 | 作用 |
| --- | --- |
| `mOpenedPages` | 保存所有已创建页面实例，以及实例对应的 Handle 和生命周期 |
| `mOpenedSinglePages` | 按页面脚本类型找到已经打开的 Single 页面 |
| `mOpeningSinglePages` | 合并同一个 Single 页面的重复异步打开请求 |
| `mPageBackStack` | 保存可以通过返回操作关闭的页面顺序 |

## 二、哪些页面进入返回栈

页面必须在 `UIPageAttribute` 中显式声明：

```csharp
addToBackStack: true
```

同时，框架还要求它满足以下条件：

- `UIOpenMode.Single`
- `UILifetime.Scene`

`addToBackStack` 必须由具体页面决定，不能仅根据 `UILayer` 推断。因为 HUD 和背包都可能位于 `Normal` 层，但 HUD 通常不应该被返回按钮关闭。

下面这些页面不进入返回栈：

- `Multiple` 页面，例如多个伤害跳字或提示实例。
- `Toast` 页面，因为短时提示不应影响返回按钮。
- `Persistent` 页面，因为它们跨场景存在，不属于某一个场景的导航历史。
- `System` 页面，例如 Loading、断线提示。

不进入返回栈不代表页面不能关闭。它们仍然可以调用：

```csharp
uiService.Close(page);
uiService.Close<SomePage>();
```

## 三、先修改 IUGUIService

修改文件：

`Assets/Framework/Runtime/UI/UGUI/IUGUIService.cs`

在 `CloseAll()` 附近增加：

```csharp
/// <summary>
/// 关闭返回栈中最后打开的页面。
/// </summary>
/// <returns>成功关闭页面时返回 true。</returns>
bool Back();
```

这个方法只处理返回栈中的页面，不应该等价于 `CloseAll()`。

业务调用方式：

```csharp
if (!uiService.Back())
{
    // 没有可以返回的页面时，再处理退出窗口或场景返回。
}
```

## 四、在 UGUIService 中增加返回栈

修改文件：

`Assets/Framework/Runtime/UI/UGUI/UGUIService.cs`

### 4.1 扩展 UIPageAttribute

修改文件：

`Assets/Framework/Runtime/UI/UGUI/UIPageAttribute.cs`

增加配置属性：

```csharp
public bool AddToBackStack { get; }
```

并给构造函数增加最后一个可选参数：

```csharp
bool addToBackStack = false
```

构造函数中保存：

```csharp
AddToBackStack = addToBackStack;
```

默认值是 `false`，这样原有页面不需要全部修改，只有真正属于导航流程的页面才显式打开它。

### 4.2 增加字段

在 `mPageAttributes` 后面增加：

```csharp
// 只保存可以通过 Back 关闭的页面，最后压入的页面是当前返回目标。
private readonly Stack<UIPage> mPageBackStack = new Stack<UIPage>();
```

这里使用的是真正的 `Stack<UIPage>`，所以入栈使用 `Push`，出栈使用 `Pop`。不要写成 `List<UIPage>` 和 `Add`。

### 4.3 修复 attribute 的作用域

`attribute` 是 `OpenAsync<T>` 内的局部变量：

```csharp
var attribute = GetPageAttribute(pageType);
```

`CompleteSingleOpen<T>` 是另一个方法，不能直接使用这个局部变量。因此，调用异步完成方法时必须把 `attribute` 传进去。

找到 `OpenAsync<T>` 末尾的资源加载代码，将完成回调修改为：

```csharp
mAssetLoader.LoadAsync<GameObject>(attribute.ResourceKey,
    handle => CompleteSingleOpen<T>(
        pageType,
        attribute,
        openParent,
        lifetime,
        sceneVersion,
        handle),
    reason => FailSingleOpen(pageType, reason));
```

然后修改 `CompleteSingleOpen<T>` 的方法签名，在 `Type pageType` 后加入 `UIPageAttribute attribute`：

```csharp
private void CompleteSingleOpen<T>(
    Type pageType,
    UIPageAttribute attribute,
    Transform parent,
    UILifetime lifetime,
    int sceneVersion,
    AssetHandle<GameObject> handle)
    where T : UIPage
```

这里传入的是本次打开请求已经读取的配置，资源异步加载完成后直接使用它即可。

### 4.4 增加判断方法

在 `UGUIService` 中增加：

```csharp
private static bool ShouldEnterBackStack(UIPageAttribute attribute)
{
    return attribute.AddToBackStack
        && attribute.OpenMode == UIOpenMode.Single
        && attribute.Lifetime == UILifetime.Scene;
}
```

这个方法只判断页面是否属于导航历史，不判断页面能不能关闭。

### 4.5 Single 页面创建完成后入栈

在 `CompleteSingleOpen<T>` 中找到已有的登记代码：

```csharp
mOpenedPages.Add(page, new OpenedPageEntry(handle, lifetime));
mOpenedSinglePages.Add(pageType, page);
mOpeningSinglePages.Remove(pageType);
page.OnOpen();
pending.NotifyOpened(page);
```

修改为：

```csharp
mOpenedPages.Add(page, new OpenedPageEntry(handle, lifetime));
mOpenedSinglePages.Add(pageType, page);
mOpeningSinglePages.Remove(pageType);

// 先登记返回记录，再调用页面回调。
// 这样页面在 OnOpen 中主动关闭自己时，Close(page) 也能清理返回记录。
if (ShouldEnterBackStack(attribute))
{
    mPageBackStack.Push(page);
}

page.OnOpen();
pending.NotifyOpened(page);
```

不要给 `OpenMultiple` 分支增加 `Push`。Multiple 页面是独立实例集合，不属于页面导航历史。Persistent 页面也不应设置 `addToBackStack: true`。

## 五、实现 Back()

在 `UGUIService` 中增加：

```csharp
public bool Back()
{
    while (mPageBackStack.Count > 0)
    {
        // Stack 后进先出，最后打开的页面最先返回。
        var page = mPageBackStack.Pop();

        // 页面可能已经被 Close(page) 或场景切换流程关闭。
        // 这种记录只需要跳过，不重复执行关闭流程。
        if (page != null && mOpenedPages.ContainsKey(page))
        {
            return Close(page);
        }
    }

    return false;
}
```

这里必须调用现有的 `Close(page)`，不能在 `Back()` 里重复写以下逻辑：

- 查找 Handle。
- 调用 `OnClose()`。
- `Destroy(page.gameObject)`。
- `Handle.Release()`。

这样可以保证普通关闭和返回关闭使用同一条资源释放路径。

## 六、主动关闭页面时清理返回栈

### 6.1 为什么不能直接调用 Remove

`mPageBackStack` 的类型是 `Stack<UIPage>`，它提供 `Push` 和 `Pop`，没有按元素删除的 `Remove(page)` 方法。

但页面可能通过关闭按钮主动调用：

```csharp
uiService.Close(this);
```

因此 `Close(UIPage page)` 关闭页面时，必须同步从返回栈移除这个页面，否则之后 `Back()` 可能取到已经销毁的实例。

### 6.2 增加 RemoveFromBackStack

在 `UGUIService` 中增加：

```csharp
private void RemoveFromBackStack(UIPage page)
{
    if (mPageBackStack.Count == 0)
    {
        return;
    }

    var temporaryStack = new Stack<UIPage>();

    // 暂时倒出原栈，跳过要删除的页面。
    while (mPageBackStack.Count > 0)
    {
        var current = mPageBackStack.Pop();
        if (!ReferenceEquals(current, page))
        {
            temporaryStack.Push(current);
        }
    }

    // 按原顺序恢复剩余页面。
    while (temporaryStack.Count > 0)
    {
        mPageBackStack.Push(temporaryStack.Pop());
    }
}
```

### 6.3 修改 Close(UIPage page)

找到现有的：

```csharp
mOpenedPages.Remove(page);
```

紧接着增加：

```csharp
RemoveFromBackStack(page);
```

最终相关顺序应为：

```csharp
mOpenedPages.Remove(page);
RemoveFromBackStack(page);

var pageType = page.GetType();
if (mOpenedSinglePages.TryGetValue(pageType, out var openedPage)
    && ReferenceEquals(openedPage, page))
{
    mOpenedSinglePages.Remove(pageType);
}

page.OnClose();
UnityEngine.Object.Destroy(page.gameObject);
entry.Handle.Release();
```

`Back()` 中已经先 `Pop()` 过当前页面，再调用 `Close(page)`。此时 `RemoveFromBackStack(page)` 找不到它也没有问题，其他页面的顺序会保持不变。

## 七、清理场景和服务时的返回栈

### 7.1 CloseAll()

在 `CloseAll()` 遍历关闭页面后增加：

```csharp
mPageBackStack.Clear();
```

这样调用 `CloseAll()` 后，不会留下已经销毁页面的导航记录。

### 7.2 CloseScenePages()

当前返回栈只保存 `UILifetime.Scene` 页面，所以场景切换关闭页面后，直接清空返回栈：

```csharp
mPageBackStack.Clear();
```

建议放在 `CloseScenePages()` 完成场景页面和正在加载页面的清理之后。

### 7.3 Deinit()

`Deinit()` 已经调用 `CloseAll()`，因此 `CloseAll()` 清空返回栈后，`Deinit()` 不需要重复实现清理逻辑。

## 八、页面 prefab 和脚本的职责

页面 prefab 和页面脚本应该一起描述一个具体页面，但职责不同。

### prefab 负责

- UI 层级结构。
- `Text`、`Image`、`Button` 等视图组件。
- 页面默认尺寸、锚点和样式。
- 可复用的动画组件和 `CanvasGroup`。
- Inspector 中可以调整的视觉参数。

### 继承 UIPage 的脚本负责

- 页面打开时初始化数据。
- 页面关闭时注销监听。
- 把按钮事件连接到页面行为。
- 调用 `IUGUIService.Close(this)` 关闭当前实例。
- 页面自己的业务交互。

### UGUIService 负责

- 根据页面类型读取 `UIPageAttribute`。
- 通过资源加载器加载 prefab。
- 选择 `UGUIRoot` 中的父层级。
- 创建并登记页面实例。
- 管理 Single、Multiple、Scene、Persistent 行为。
- 管理返回栈。
- 关闭页面并释放 Handle。

页面脚本不应该自己加载 prefab，也不应该直接操作 `mOpenedPages` 或 `mPageBackStack`。

## 九、参考 SKCell 后的取舍

本地 SKCell 中的 `SKUIPanelManager`、`SKUIPanel` 和 `SKUIAnimation` 可以借鉴以下思路：

- 页面 prefab 自带视图结构和动画配置。
- 页面脚本负责页面行为。
- 通用按钮、动画和 Tooltip 可以独立做成可复用 prefab。
- 父页面可以管理子页面的显示状态。

当前框架暂时不直接采用这些机制：

- 扫描 `Resources` 文件夹并根据 prefab 文件名数字建立页面 ID。
- 使用全局静态字典保存所有页面实例。
- 用一个通用面板状态系统替代 `UIPage + UGUIService`。

原因是当前框架已经有：

- 泛型 `OpenAsync<T>()`。
- `UIPageAttribute`。
- `IAssetLoader`。
- 页面实例到 `AssetHandle` 的对应关系。

再加入数字 ID 和全局静态注册，会让同一个页面同时拥有“脚本类型”和“数字 ID”两套身份系统。

## 十、参考 Square Inch Stage 后的取舍

`Square Inch Stage` 中有一些适合业务 UI 的做法：

- Tooltip 根据目标位置动态定位，并限制在 Canvas 范围内。
- `CanvasGroup` 同时控制透明度、交互和射线阻挡。
- 转场黑幕使用独立 Canvas 和较高的 `sortingOrder`。
- 某些 UI prefab 可以作为业务功能组件被单独复用。

这些功能暂时放在表现组件或业务项目中，不放进 `UGUIService`：

- Tooltip 定位属于 Tooltip 组件。
- 淡入淡出属于动画或 `CanvasGroup` 组件。
- 黑幕转场属于场景转场服务。
- 业务 prefab 属于项目自己的 UI 组件库。

`UGUIService` 只需要关心页面资源、页面实例、父层级、生命周期和导航顺序。

## 十一、Prefab 组件库的后续目录

未来确实需要通用 UI 组件时，可以单独建立：

```text
Assets/Framework/Prefabs/UI/Common/
```

适合放入：

- 通用按钮。
- 通用弹窗背景。
- Loading 遮罩。
- Tooltip 基础视图。
- 带 `CanvasGroup` 的淡入淡出组件。

这些组件由页面 prefab 组合使用，`UGUIService` 不需要知道它们的具体名称。

## 十二、验证顺序

实现后按下面顺序验证：

1. 打开两个 `Single + Scene` 页面。
2. 调用一次 `Back()`，确认只关闭最后打开的页面。
3. 手动调用 `Close(page)`，再次调用 `Back()`，确认不会取到已关闭页面。
4. 连续打开多个 `Multiple` 页面，调用 `Back()`，确认它们不会被返回栈关闭。
5. 打开 `Persistent` 页面，调用 `Back()`，确认它仍然存在。
6. 切换场景，确认场景页面销毁，返回栈也被清空。
7. 调用 `CloseAll()`，确认所有页面和返回记录都被清理。

示例项目中的导航页面写法：

```csharp
[UIPage("UI/ExampleScenePage", UIOpenMode.Single,
    UILayer.Popup, UILifetime.Scene, addToBackStack: true)]
```

示例文件：

- `Assets/Example/Scripts/ExampleScenePage.cs`：导航页面 A。
- `Assets/Example/Scripts/ExampleDetailPage.cs`：导航页面 B。
- `Assets/Example/Scripts/ExampleControlPanel.cs`：打开 A、打开 B、调用 `Back()`。

运行示例时依次点击“打开导航页面 A”“打开导航页面 B”“返回上一页”，B 应该先关闭；再次点击返回，A 才会关闭。

## 小结

返回栈的最终实现只有一种：`Stack<UIPage>`。打开页面使用 `Push`，返回使用 `Pop`，普通关闭使用 `RemoveFromBackStack` 移除指定页面。`mOpenedPages` 继续负责实例和 Handle，返回栈只负责导航顺序。

Prefab 方面先保持“页面 prefab 保存视图，页面脚本处理交互，UGUIService 管理生命周期”的边界。SKCell 和 `Square Inch Stage` 中的通用动画、Tooltip、转场和组件 prefab，可以在确实出现重复需求时单独加入组件库。
