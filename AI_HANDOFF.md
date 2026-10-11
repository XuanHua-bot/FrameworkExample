# FrameworkExample 当前交接文档

> 本文件记录 `E:\unityXiangmu\FrameworkExample` 的当前状态。
> 稳定工作规则见同目录 `AGENTS.md`。
>
> ⚠️ **2026-10-09 起，UI / 组件库相关的当前决策、清理清单与推进计划见同目录 `AI_HANDOFF_UI.md`。**
> 本文件下方部分内容已过时（例如 §3.4 提到的 `ExampleScenePage` / `ExamplePersistentPage` /
> `ExampleDetailPage` / `ExampleMultiplePage` 均已不存在），阅读时以实际源码与 `AI_HANDOFF_UI.md` 为准。

## 1. 项目与资料边界

### 当前项目

- 项目：`E:\unityXiangmu\FrameworkExample`
- Unity：`2021.3.44f1c1`
- 当前示例目录：`Assets\Example`
- 框架目录：`Assets\Framework`
- UI 文档目录：`Assets\Framework\Docs`

### 已发现的交接文件冲突

- 找到的旧交接文件：`E:\unityXiangmu\ShootingEditor2D\AI_HANDOFF.md`
- 该文件记录的是 `ShootingEditor2D` 的课程学习和旧项目状态。
- 当前讨论的实际项目是 `FrameworkExample`，所以没有把旧项目的课程状态当作当前框架状态。
- 当前项目原来没有 `AGENTS.md`，也没有根目录 `AI_HANDOFF.md`；本次创建了这两份文件。
- 旧 `ShootingEditor2D\AI_HANDOFF.md` 未修改，避免误改另一个项目的交接资料。

## 2. 当前框架定位

这是一个独立的通用游戏框架示例工程，不以 `ShootingEditor2D` 的业务内容为框架基准。

当前建设重点是 UGUI，目标是形成可实际使用的流程：

```text
资源加载 -> 创建页面 -> 显示和交互 -> 页面导航 -> 返回或关闭 -> 释放资源
```

长期目标还包括资源、场景、事件、输入、音频、存档等基础模块。UI Toolkit 作为未来实现方向保留边界，当前没有把两套 UI 强行合并。

## 3. 已实现的 UGUI 能力

### 3.1 页面与资源

相关文件：

- `Assets\Framework\Runtime\UI\UGUI\UIPage.cs`
- `Assets\Framework\Runtime\UI\UGUI\UIPageAttribute.cs`
- `Assets\Framework\Runtime\UI\UGUI\UIPageOptions.cs`
- `Assets\Framework\Runtime\UI\UGUI\UGUIService.cs`
- `Assets\Framework\Runtime\UI\UGUI\IUGUIService.cs`

当前能力：

- 页面脚本继承 `UIPage`。
- `UIPageAttribute` 配置资源路径、`UIOpenMode`、`UILayer`、`UILifetime`。
- `UGUIService` 通过 `IAssetLoader` 加载 Prefab。
- `AssetHandle<GameObject>` 与具体页面实例一一登记。
- 页面关闭时执行 `OnClose`、销毁实例并释放自己的 Handle。
- `Single` 页面按页面类型复用实例。
- `Multiple` 页面每次创建独立实例。
- Single 页面正在加载时由 `PendingOpen` 合并等待回调，避免重复加载和重复实例。

### 3.2 页面生命周期

`UIPage` 当前提供：

```text
OnOpen  页面创建、登记完成后调用一次
OnShow  页面显示时调用
OnHide  页面隐藏时调用
OnClose 页面销毁前调用一次
```

`CreateAsync` 只创建并登记页面，页面保持隐藏；`OpenAsync` 创建后再显示。这一点是导航服务能够先写入页面数据、再显示页面的基础。

### 3.3 Root 与层级

相关文件：

- `Assets\Framework\Runtime\UI\UGUI\UGUIRoot.cs`
- `Assets\Framework\Prefabs\UI\UGUIRoot.prefab`

当前 Root 包含：

```text
BackgroundLayer
NormalLayer
PopupLayer
SystemLayer
ToastLayer
```

`UGUIRoot` 只保存 Canvas 和层级 Transform，不负责页面生命周期、资源加载或页面遮罩判断。

### 3.4 场景与跨场景页面

`UILifetime` 当前有两种：

- `Scene`：场景切换时由 `UGUIService` 关闭。
- `Persistent`：场景切换时保留。

`UGUIService` 当前通过 `SceneManager.activeSceneChanged` 监听场景变化，并使用 `mSceneVersion` 处理场景切换期间仍未完成的页面加载请求。

示例页面：

- `ExampleScenePage`：`Scene`
- `ExamplePersistentPage`：`Persistent`
- `ExampleDetailPage`：`Scene`
- `ExampleMultiplePage`：`Multiple`

### 3.5 返回导航

相关文件：

- `Assets\Framework\Runtime\UI\Navigation\IUINavigationService.cs`
- `Assets\Framework\Runtime\UI\Navigation\UINavigationService.cs`

当前导航服务：

- `PushAsync<T>`：打开或复用页面，并把页面实例加入导航列表。
- `PushAsync<T, TData>`：打开页面并在显示前写入数据。
- `Back()`：关闭顶部页面，显示前一个页面。
- `BackTo<T>()`：关闭目标页面之上的页面，显示目标页面。
- 当前使用 `List<UIPage>` 保存底部到顶部的导航顺序。
- 导航服务不区分 Single/Multiple 的具体创建逻辑，由 `UGUIService.CreateAsync` 根据 Attribute 决定。
- 页面主动调用 `IUGUIService.Close(this)` 后，通过 `pageClosed` 事件从导航列表移除。

这里的 `List<UIPage>` 是当前已确定的设计，不再在 `Stack` 和 `List` 之间反复切换：因为需要支持 `BackTo<T>`、指定实例移除和页面主动关闭，列表更直接。

### 3.6 页面参数

相关文件：

- `Assets\Framework\Runtime\UI\UIDataContracts.cs`
- `Assets\Framework\Runtime\UI\Navigation\IUINavigationService.cs`
- `Assets\Framework\Runtime\UI\Navigation\UINavigationService.cs`

当前使用：

```csharp
IUIData
IUIPageDataReceiver<TData>
```

页面通过 `IUIPageDataReceiver<TData>.SetData(TData data)` 接收打开参数，泛型约束保证调用方传入正确的数据类型。

## 4. 最近完成并已验证的修改

### 页面级输入遮挡

已修改：

- `UIPage.cs`
  - 增加 `mBlocksUnderlyingInput`。
  - 增加 `mInputBlocker`。
  - 增加 `BlocksUnderlyingInput`。
  - 增加 `SetInputBlocker(bool)`。
- `UGUIService.cs`
  - `Show` 调用 `page.SetInputBlocker(true)`。
  - `Hide` 调用 `page.SetInputBlocker(false)`。
  - 删除根据 `UILayer.Popup/System` 推断遮挡的逻辑。
- `UGUIRoot.cs`
  - 删除 Root 级 `mPopupBlocker` 和 `SetPopupBlocker`。
- `UGUIRoot.prefab`
  - 删除旧的 Root 级 `PopupBlocker` 对象和序列化引用。

使用规则：需要阻挡底层输入的页面 Prefab 自己携带全屏透明 `Image/Panel`，并把它放在页面视觉内容下方；页面的 `UIPage` 组件勾选 `Blocks Underlying Input`，绑定 `InputBlocker`，并保持 Graphic 的 `Raycast Target` 开启。

重要边界：`Normal` 页面也可以遮挡输入，`Popup` 页面也不必然遮挡输入；`UILayer` 和输入遮挡是两个独立概念。

### 验证结果

已执行：

```text
dotnet build E:\unityXiangmu\FrameworkExample\Assembly-CSharp.csproj --no-restore
```

结果：

- `Assembly-CSharp` 成功生成。
- 0 个编译错误。
- 存在 5 个已有依赖版本冲突警告，涉及 `System.ValueTuple`、`System.Net.Http`、`System.IO.Compression`，不是本次 UI 修改引入的 C# 错误。
- 已通过静态搜索确认旧的 `SetPopupBlocker`、`RefreshPopupBlocker` 和 `mPopupBlocker` 不再出现在运行时代码和 Root Prefab 中。
- Unity 编辑器中的实际点击遮挡行为尚未在本次交接整理中重新运行确认。

## 5. 已否决或已废弃的方案

### Root 级共享 PopupBlocker

已否决。旧方案把透明遮罩放在 `UGUIRoot.PopupLayer`，再由 `UGUIService` 根据 Popup/System 层页面统一开关。

否决原因：

- Normal 层的背包、设置页也可能需要阻挡底层输入。
- 共享遮罩的层级可能高于页面自身按钮，导致页面自己的交互被遮住。
- 它把 `UILayer` 错当成了输入策略。

旧文档：

- `Assets\Framework\Docs\UIPopupBlockerLesson.md`

该文档已标记为废弃，后续需要按当前页面级实现重写。

### UIElementNavigator 局部导航方案

目前不进入运行时代码。页面内部的 Tab、子面板和登录/注册等内容，先由具体页面脚本和 Prefab 自己管理；只有真实项目出现多个页面内部导航需求后，再选择最小的组件级方案。

旧文档：

- `Assets\Framework\Docs\UIPageElementNavigationLesson.md`

该方案不能作为当前已实现能力。

### 把 UGUI 与 UI Toolkit 立即合并成一套完整 UI 抽象

暂缓。当前 UGUI 依赖 Canvas、RectTransform、Graphic Raycast 和 GameObject Prefab；UI Toolkit 依赖 UIDocument、VisualElement、UXML、USS 和 PanelSettings。现在强行统一会扩大修改面，且当前没有真实 UIToolkit 使用场景需要验证。

### 把对象池直接塞进 UGUIService

暂缓。普通页面和高频伤害跳字的生命周期、重置要求和资源引用方式不同。高频临时 UI 将来单独设计复用池，不直接改变普通页面的关闭语义。

### 现在直接接入 YooAsset、HybridCLR 等第三方方案

暂缓。核心资源入口保持 `IAssetLoader`，具体第三方库以后放在可选适配层；当前 `ResourcesAssetLoader` 已足够支撑示例项目。

## 6. 外部方案调查结论

这些内容是设计参考，不表示本项目已经接入对应框架。

- UnityGameFramework/GameFrameX：UI 层级、页面生命周期、覆盖/暂停行为可以分开；适合参考 `OnPause`、`OnResume`、`OnCover`、`OnReveal` 的边界。
- QFramework：`UILevel`、`PanelOpenType`、UI 栈和页面参数是分开的；适合参考调用层减少 Single/Multiple 判断，不直接复制其完整 UIKit。
- SKCell：`SKUIPanel`、Root/Leaf、单 Leaf 激活和面板动画值得参考；它的公开资料没有证明本项目需要一个全局模态遮挡字段。
- Unity 官方 UGUI：透明 Graphic 的 `Raycast Target` 和 `CanvasGroup.blocksRaycasts` 是输入拦截的基础机制。

采用原则：只吸收已确认适合当前代码边界的设计，不因为外部框架存在某个模块就自动加入本项目。

## 7. 当前讨论过但尚未实施的下一步

### 候选：最小版 SceneService

上一轮讨论建议先做独立的场景服务，目标是把场景加载从 UI 服务中分离：

```text
SceneService 负责场景加载和场景事件
UGUIService 负责根据 UILifetime 清理页面
```

当前状态：仅讨论过，尚未创建接口、实现类或示例；不能把它写成已完成能力。

在实施前需要再次确认：

1. 是否真的需要由框架统一封装场景加载，而不是当前示例直接调用 `SceneManager`。
2. 是否先只支持 `LoadSceneAsync` 的 Single 模式。
3. 场景切换通知由 SceneService 发布，还是继续由 UGUIService 监听 Unity 事件。
4. 如何避免场景服务和 Architecture 生命周期互相持有。

如果上述问题没有真实使用需求，继续保持当前 `UGUIService` 的场景监听也可以，不必为了拆模块而拆模块。

## 8. 当前文件状态说明

### 当前有效的主要文档

- `UIFrameworkTeaching.md`：当前 UI 总体讲解，但部分历史章节需要继续按实际源码校对。
- `UIOpenModeAgnosticNavigationLesson.md`：Single/Multiple 对调用方透明的导航设计。
- `UIPageDataLesson.md`：页面参数和导航数据传递。
- `UINavigationServiceLesson.md`：导航服务历史修改说明，阅读时必须以当前源码为准。
- `UIFrameworkLongTermGoal.md`：长期目标和设计原则。

### 已废弃或需要警惕的文档

- `UINavigationServiceLessonV2.md`：已明确标记废弃。
- `UIPageElementNavigationLesson.md`：局部导航器方案只讨论过，未实现。
- `UIPopupBlockerLesson.md`：旧 Root 级共享遮罩方案已废弃。
- `UIBackStackAndPrefabLesson.md`：包含历史版本的返回栈和 Attribute 设计，不能脱离当前源码直接照抄。

## 9. 交接时的下一步工作方式

1. 先读本文件和 `AGENTS.md`。
2. 如果继续 UI，先读取当前 `UGUIService.cs`、`UIPage.cs`、`UINavigationService.cs` 和对应示例 Prefab。
3. 对历史教学文档只取设计背景，不把旧代码视为当前代码。
4. 在实现新模块前，先给出“当前问题、成熟方案对照、采用/暂缓理由、最小修改范围”。
5. 用户未明确要求改代码时，只写方案或教学文档。
6. 每次修改后执行适合当前改动的静态检查或编译，并把 Unity 编辑器尚未验证的部分单独列出。
