# UI 导航服务：按当前代码补完剩余修改

## 当前模块进度（3/4）

本文依据当前工作区实际文件重写。已经改好的内容不再重复。

### 已完成，不要再改

- `UIPage` 已有 `OnOpen`、`OnShow`、`OnHide`、`OnClose`。
- `IUGUIService` 已声明 `CreateAsync`、`Show`、`Hide` 和 `pageClosed`。
- `UGUIService` 已有 `Show`、`Hide`，`OpenAsync` 也已经调用 `CreateAsync`。
- `LoadAndCreate` 和 `CompleteSingleOpen` 已经在创建实例后调用 `SetActive(false)`。
- `UIPageAttribute.AddToBackStack` 已删除。
- `IUINavigationService` 和 `UINavigationService` 已创建。
- `BackTo` 的循环当前已经是 `i--`，不要再次修改。
- 两个示例页面的 `addToBackStack: true` 已删除。

### 当前真正剩余的问题

1. `CreateAsync` 的回调类型仍是 `Action<object>`，与接口的 `Action<T>` 不一致。
2. `Close` 中的 `entry` 已经失去声明，但结尾仍使用 `entry.Handle.Release()`。
3. 示例 Architecture 没有注册导航服务。
4. 示例控制面板仍使用 `IUGUIService.OpenAsync`，返回按钮没有绑定。
5. `ExampleDetailPage` 仍然直接关闭自己，没有调用导航返回。

下面只处理这五项。

---

## 一、修复 `UGUIService.CreateAsync` 的签名

文件：

`E:/unityXiangmu/FrameworkExample/Assets/Framework/Runtime/UI/UGUI/UGUIService.cs`

当前第 157 行附近是：

```csharp
public void CreateAsync<T>(Action<object> onCreated, Action<string> onFailed=null) where T : UIPage
```

这里不要改方法体，只改这一行的参数类型和格式。

删除当前这一行：

```csharp
public void CreateAsync<T>(Action<object> onCreated, Action<string> onFailed=null) where T : UIPage
```

在原位置新增：

```csharp
public void CreateAsync<T>(
    Action<T> onCreated,
    Action<string> onFailed = null)
    where T : UIPage
```

只改变了 `Action<object>` 到 `Action<T>`。原因是接口声明为：

```csharp
void CreateAsync<T>(Action<T> onCreated, Action<string> onFailed = null)
```

如果仍然使用 `Action<object>`，下面三处都会出现类型错误：

```text
OpenMultiple(..., onCreated, ...)
AddPendingCallbacks(pending, onCreated, ...)
onCreated?.Invoke((T)openedPage)
```

因为它们需要的是当前泛型页面类型 `T` 的回调。

### 不要改动的部分

`CreateAsync` 的方法体已经包含正确的 Single、Multiple、PendingOpen 和加载逻辑，本次不要再次整段替换。只修改方法签名。

---

## 二、修复 `Close` 中缺失的 `entry`

文件仍然是：

`E:/unityXiangmu/FrameworkExample/Assets/Framework/Runtime/UI/UGUI/UGUIService.cs`

当前 `Close` 开头是：

```csharp
public bool Close(UIPage page)
{
    Hide(page);
    mOpenedPages.Remove(page);
```

但方法结尾仍然有：

```csharp
entry.Handle.Release();
```

当前 `entry` 没有声明，所以这是一个确定的编译错误。

### 删除的代码

在 `Close(UIPage page)` 内，从方法开头的 `{` 到 `Hide(page);` 之间，保留方法声明，但删除当前这段：

```csharp
Hide(page);
mOpenedPages.Remove(page);
```

### 新增的代码

在方法声明后，加入完整的页面和 Handle 查询：

```csharp
public bool Close(UIPage page)
{
    // 先找到实例对应的 Handle；找不到时不能继续访问 page 或 entry。
    if (page == null || !mOpenedPages.TryGetValue(page, out var entry))
    {
        return false;
    }

    // Single 和 Multiple 都要执行隐藏生命周期。
    Hide(page);
    mOpenedPages.Remove(page);
```

### 保留后面的代码

从下面这行开始的代码保留：

```csharp
var pageType = page.GetType();
```

但是确认 Single 判断内部只保留删除 Single 字典：

```csharp
if (mOpenedSinglePages.TryGetValue(pageType, out var openedPage)
    && ReferenceEquals(openedPage, page))
{
    mOpenedSinglePages.Remove(pageType);
}
```

方法结尾保留为：

```csharp
page.OnClose();
pageClosed?.Invoke(page);

// 销毁实例并释放该实例持有的 Handle。
UnityEngine.Object.Destroy(page.gameObject);
entry.Handle.Release();
return true;
```

最终 `Close` 的完整顺序是：

```text
TryGetValue 得到 entry
    -> Hide(page)
    -> 从 mOpenedPages 移除
    -> 从 mOpenedSinglePages 移除（如果是 Single）
    -> OnClose
    -> pageClosed
    -> Destroy
    -> entry.Handle.Release
```

这里 `entry` 保存的是当前页面实例对应的 Handle。Multiple 页面也必须经过同一套释放流程。

---

## 三、注册 `UINavigationService`

文件：

`E:/unityXiangmu/FrameworkExample/Assets/Example/Scripts/ExampleArchitecture.cs`

当前 `Init` 是：

```csharp
protected override void Init()
{
    RegisterUtility<IAssetLoader>(new ResourcesAssetLoader());
    RegisterUtility<IUGUIService>(new UGUIService(GetUtility<IAssetLoader>()));
}
```

### 新增代码

在 `IUGUIService` 注册语句之后新增：

```csharp
// 导航服务依赖已经注册的 IUGUIService。
RegisterUtility<IUINavigationService>(
    new UINavigationService(GetUtility<IUGUIService>()));
```

最终顺序：

```text
IAssetLoader
    -> IUGUIService
    -> IUINavigationService
```

不要在 `UINavigationService` 内部创建新的 `UGUIService`。

---

## 四、修改 `ExampleControlPanel`

文件：

`E:/unityXiangmu/FrameworkExample/Assets/Example/Scripts/ExampleControlPanel.cs`

当前字段 `mBackButton` 已经存在，当前 `Awake` 和 `OnDestroy` 没有使用它。

### 1. 新增返回按钮绑定

在 `Awake()` 中，建议放在两个导航入口绑定之后：

```csharp
mOpenSceneButton.onClick.AddListener(OpenScenePage);
mOpenDetailButton.onClick.AddListener(OpenDetailPage);
mBackButton.onClick.AddListener(Back);
```

在 `OnDestroy()` 中对应加入：

```csharp
mBackButton.onClick.RemoveListener(Back);
```

### 2. 新增 `Back` 方法

在 `OpenDetailPage()` 后面加入：

```csharp
private void Back()
{
    if (!this.GetUtility<IUINavigationService>().Back())
    {
        Debug.Log("当前已经是导航根页面，或导航正在加载。");
    }
}
```

### 3. 修改两个入口方法

在 `OpenScenePage()` 中，只替换调用对象和方法：

删除：

```csharp
this.GetUtility<IUGUIService>().OpenAsync<ExampleScenePage>(
```

新增：

```csharp
this.GetUtility<IUINavigationService>().PushAsync<ExampleScenePage>(
```

在 `OpenDetailPage()` 中，删除：

```csharp
this.GetUtility<IUGUIService>().OpenAsync<ExampleDetailPage>(
```

新增：

```csharp
this.GetUtility<IUINavigationService>().PushAsync<ExampleDetailPage>(
```

两个方法中的成功和失败回调保持原样。

以下三个方法不要改：

```text
OpenPersistentPage
OpenMultiplePage
SwitchScene
```

---

## 五、修改 `ExampleDetailPage`

文件：

`E:/unityXiangmu/FrameworkExample/Assets/Example/Scripts/ExampleDetailPage.cs`

当前 `CloseSelf()` 是：

```csharp
private void CloseSelf()
{
    // 主动关闭时，UGUIService 会同步移除 B 的返回栈记录。
    this.GetUtility<IUGUIService>().Close(this);
}
```

### 删除

删除方法内的注释和这一行：

```csharp
this.GetUtility<IUGUIService>().Close(this);
```

### 新增

在原位置写成：

```csharp
private void CloseSelf()
{
    // B 是当前导航流程顶部，关闭按钮的语义是返回 A。
    this.GetUtility<IUINavigationService>().Back();
}
```

`ExampleScenePage` 是当前流程根页面，本次保留它的 `IUGUIService.Close(this)`。

---

## 六、当前不需要改动的文件

- `IUGUIService.cs`：接口声明已经完成。
- `UIPageAttribute.cs`：`AddToBackStack` 已删除。
- `UIPage.cs`：生命周期已经完成。
- `UINavigationService.cs`：`BackTo` 已经是 `i--`，本次不改。
- `UGUIService.Show` 和 `UGUIService.Hide`：当前实现先保留。
- `ExampleScenePage.cs` 的 Attribute：已经删除 `addToBackStack`。

---

## 七、完成后的全文检查

在项目中搜索以下内容：

```text
1. UGUIService 的 CreateAsync 使用 Action<T>，不能是 Action<object>。
2. Close 方法中存在 out var entry。
3. entry.Handle.Release() 能访问到同一个 entry。
4. CompleteSingleOpen 中 pending.NotifyOpened(page) 只出现一次。
5. ExampleArchitecture 注册 IUINavigationService。
6. ExampleControlPanel 的 mBackButton 有 AddListener 和 RemoveListener。
7. A、B 页面入口使用 PushAsync。
8. ExampleDetailPage.CloseSelf 使用 IUINavigationService.Back。
9. 运行时代码中不存在 AddToBackStack。
```

---

## 八、最小验证

```text
打开 A
    -> A.OnOpen -> A.OnShow

打开 B
    -> B.OnOpen
    -> A.OnHide
    -> B.OnShow

点击返回
    -> B.OnHide -> B.OnClose -> pageClosed
    -> B.Destroy -> Handle.Release
    -> A.OnShow
```

另外确认：

- A 显示时再次点击返回，返回 false。
- B 资源加载失败时，A 仍然显示。
- 两个 Multiple 页面可以同时存在。
- 关闭 Multiple 页面时也调用 OnHide，并释放它自己的 Handle。
- 切换场景后 Scene 页面从导航历史中移除。
- Persistent 页面按原规则保留。

## 总结

当前剩余修改只有：

```text
UGUIService.CreateAsync：Action<object> -> Action<T>
UGUIService.Close：恢复 entry 的 TryGetValue 声明
ExampleArchitecture：注册 UINavigationService
ExampleControlPanel：绑定返回按钮并改用 PushAsync
ExampleDetailPage：改用导航 Back
```

本次只修改教学文档，没有修改项目源代码。
