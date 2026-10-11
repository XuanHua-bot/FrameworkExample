# Popup 模态遮挡与栈顶交互：下一步实现教程

## 这一步要实现什么

本教程只实现两个行为：

- Popup 页面显示后，点击弹窗外部不会穿透到底层按钮。
- 多个模态 Popup 同时显示时，只允许最上面的 Popup 交互。

完成后的效果：

- `ExampleScenePage` 在下层时保持显示，但暂停交互。
- `ExampleDetailPage` 在上层时可以正常点击。
- 关闭 `ExampleDetailPage` 后，`ExampleScenePage` 恢复交互。
- 关闭全部 Popup 后，底层控制面板恢复点击。

本教程不实现点击遮罩关闭、动画、键盘焦点、游戏输入锁定和新的资源加载系统。

## 修改范围

本次只修改以下内容：

1. `ExampleScenePage.prefab` 和 `ExampleDetailPage.prefab`：以 `Assets/Example/Resources/UI/Popup - Size Fitter.prefab` 为模板。
2. `UGUIService.cs`：维护可见模态页面顺序，并控制栈顶交互。

`UIPage.cs` 已有的 `BlocksUnderlyingInput`、`SetInputBlocker()` 和 `SetInteractable()` 直接复用，不在本教程重复添加。

> 当前项目已将两个 `UILayer.Popup` 页面按这个模板改造。模板里的资源包 `UltimateClean.Popup` 组件已移除，页面生命周期仍由 `UIPage` 和 `UGUIService` 管理；否则关闭时会再次进入资源包自己的 `RemoveBackground()`。

## 1. 先改 Popup Prefab 的结构

目标文件：

    Assets/Example/Resources/UI/ExampleScenePage.prefab
    Assets/Example/Resources/UI/ExampleDetailPage.prefab

在 Unity Prefab Mode 中处理，每个 Prefab 都改成：

    ExampleScenePage（根 RectTransform：铺满 PopupLayer）
    ├── InputBlocker（全屏透明 Image，排在内容之前）
    └── Content（原来约 420 × 180 的深色面板）
        ├── Message
        ├── OpenDetailButton
        └── CloseButton

`ExampleDetailPage` 同理，只保留它原来的 `Message` 与 `BackButton`，并让 `Message` 排在 `BackButton` 前面：该页面当前用 `GetComponentInChildren<Text>()` 取得第一条文本。**不要删现有按钮，也不要替换 Controller。** 把原根节点的 `Image` 移到 `Content`，按钮、文字节点一起放入 `Content`，原有按钮和文字组件的序列化引用保持原样；如果 Unity 因拖动节点丢失引用，在根节点的页面 Controller 上重新拖入对应字段。

根 `RectTransform`：

    Anchor Min  (0, 0)
    Anchor Max  (1, 1)
    Left/Right/Top/Bottom  0
    Image 组件移至 Content；根节点保留 UIPage Controller，并添加 CanvasGroup（若已存在则沿用）

`Content`：锚点置于中心，大小沿用原面板尺寸（可从修改前 Prefab 的根 `RectTransform` 复制）。这样铺满屏幕的是页面的交互范围，视觉弹窗仍居中保持原尺寸。Dark 主题 Sprite 仍放在 `Content` 的 Image 上。

`InputBlocker`：

    RectTransform：拉伸铺满根节点，四边 Offset = 0
    Image Color：(0, 0, 0, 0)；需要暗化时可以把 Alpha 设为 0.5
    Image Raycast Target：开启
    默认 Active：关闭
    Sibling 顺序：必须在 Content 前面

在根节点 `UIPage` 组件上设置：

    Blocks Underlying Input = true
    Input Blocker = 刚创建的 InputBlocker
    Canvas Group = 根节点 CanvasGroup

`ExampleMultiplePage` 位于 ToastLayer，`ExamplePersistentPage` 位于 SystemLayer，本次先不将它们设为模态。尤其不要因为 `SystemLayer` 名称就自动拦截输入。

## 2. 修改 UGUIService：记录可见模态页

文件：

    Assets/Framework/Runtime/UI/UGUI/UGUIService.cs

### 2.1 增加一个字段

在 `mOpenedPages` 字段后加入：

    // 只记录当前可见、声明 BlocksUnderlyingInput 的页面；顺序为底到顶。
    private readonly List<UIPage> mVisibleModalPages = new List<UIPage>();

这里直接使用文件已有的 `System.Collections.Generic`，不用新增 `using`。

### 2.2 用下面代码替换 `Show(UIPage page)` 方法

    public void Show(UIPage page)
    {
        if (page == null || !mOpenedPages.ContainsKey(page)) return;

        // Single 页面再次 Show 时也应该被提到本层最前面。
        page.transform.SetAsLastSibling();

        // 重新显示已有模态页时，将它移动到模态顺序末尾。
        mVisibleModalPages.Remove(page);
        if (page.BlocksUnderlyingInput)
            mVisibleModalPages.Add(page);

        if (!page.gameObject.activeSelf)
        {
            page.gameObject.SetActive(true);
            page.SetInputBlocker(true);
            RefreshModalInteraction();
            page.OnShow();
            return;
        }

        RefreshModalInteraction();
    }

保留 `OnShow()` 只在隐藏转显示时调用的原语义，重复打开已经显示的 Single 页面只调整排序。

### 2.3 用下面代码替换 `Hide(UIPage page)` 方法

    public void Hide(UIPage page)
    {
        if (page == null || !mOpenedPages.ContainsKey(page)) return;

        if (page.gameObject.activeSelf)
        {
            page.OnHide();
            page.SetInputBlocker(false);
            page.gameObject.SetActive(false);
        }

        mVisibleModalPages.Remove(page);
        RefreshModalInteraction();
    }

`Close(UIPage page)` 已调用 `Hide(page)`，所以关闭、`CloseAll()` 和场景切换时不需要重复写一次模态列表清理。它原有的 `OnClose`、实例销毁、`AssetHandle.Release()` 代码全部保留。

### 2.4 在 `Hide` 后增加刷新方法

    private void RefreshModalInteraction()
    {
        var topIndex = mVisibleModalPages.Count - 1;
        for (var i = 0; i < mVisibleModalPages.Count; i++)
        {
            var page = mVisibleModalPages[i];
            page.SetInteractable(i == topIndex);
        }
    }

`SetInteractable(false)` 会让下层模态页的 `CanvasGroup` 停止接收射线；最上层页面保持可交互，它自己的 `InputBlocker` 负责拦截弹窗外的点击。关闭顶部页后 `Hide` 将其移出列表，下层页重新变为顶部并恢复交互。

注意：该方法只控制 **模态页之间** 的交互。普通页面并没有被强行 `SetInteractable(false)`；它们的鼠标/触摸点击由最上层全屏 `InputBlocker` 拦截。键盘/手柄选中对象需要另做焦点管理，不能把这里的射线遮挡当成完整输入锁。

## 3. 核对 UIPage：不需要修改

文件：

    Assets/Framework/Runtime/UI/UGUI/UIPage.cs

下面三个成员已经存在，手抄本教程时只需要确认它们存在，**不要重复添加**：

    public bool BlocksUnderlyingInput => mBlocksUnderlyingInput;
    public virtual void SetInputBlocker(bool active) { /* 当前实现保留 */ }
    public virtual void SetInteractable(bool interactable) { /* 当前实现保留 */ }

其中 `SetInputBlocker(true)` 只有在 Prefab 的 `mInputBlocker` 已绑定且 `mBlocksUnderlyingInput == true` 时才会激活遮挡。这个教程因此先配置 Prefab，再修改服务，不新增 `UGUIRoot` 级共享遮罩。

## 4. 验证流程

### 编译

Unity 等待脚本编译完成，Console 不应出现新的 C# 错误。项目外部也可运行：

    dotnet build E:\unityXiangmu\FrameworkExample\Assembly-CSharp.csproj --no-restore --verbosity:minimal

该命令只验证 C#，不能代替 Unity 内的 Prefab 排序与射线测试。

### 场景手测

1. 运行 `FrameworkExample` 场景，控制面板按钮应正常工作，启动时不自动弹出额外页面。
2. 点击打开 `ExampleScenePage`。其 `InputBlocker` 激活，面板外的控制面板按钮无法点击，弹窗自己的按钮仍能点击。
3. 从 A 页进入 `ExampleDetailPage`。现有导航会把 A 页隐藏，B 页可交互；点击 B 的返回按钮后，A 页恢复。
4. 关闭 A 页。遮挡对象随页面销毁，控制面板按钮恢复点击。
5. 打开 `ExampleMultiplePage`，它仍是非模态 Toast，可重复创建，不应启用 Popup 遮挡。

**两个同时可见的模态页** 需要单独验证：先用 `IUGUIService.OpenAsync<ExampleScenePage>()` 打开 A，再用 `IUGUIService.OpenAsync<ExampleDetailPage>()` 打开 B（此处不要经由 `UINavigationService.PushAsync`，因为导航本来就会隐藏 A）。在运行时检查：只有 B 的按钮可交互；调用 `IUGUIService.Close(B实例)` 后，A 的按钮恢复。这个测试只验证模态排序，不改变页面正式的导航流程。

### 判定表

| 情况 | 预期 |
| --- | --- |
| 没有模态页 | 底层按钮可点击 |
| A 显示 | A 可点击；A 外的底层按钮被拦截 |
| A、B 同时显示 | B 可点击；A 不响应 |
| 关闭 B | A 恢复点击 |
| 关闭 A | 底层恢复点击 |
| Toast 显示 | 不影响底层输入 |

## 5. 实现完成后的边界

本次只改 `UGUIService` 的 `Show`、`Hide` 与一个刷新方法，并在两个现有 Popup Prefab 内添加 `InputBlocker`、调整根布局。`UINavigationService` 不改，`UGUIRoot` 不改，资源包 Prefab 不改。若任何弹窗视觉盖住整个屏幕，先检查是否把原根 Image 留在了铺满全屏的新根上；它应移到定尺寸的 `Content`。
