# FrameworkExample · UI 重做交接文档

> 阅读顺序：本文件 → `AI_HANDOFF.md`（框架核心状态）→ `AGENTS.md`（长期工作规则）→ `CODELY.md`（历史记忆）
>
> 本文件记录 **2026-10-09** 的当前决策、清理清单、难度归因与推进计划。
> 本文件写作时 **尚未执行任何删除或新建**，所有"待删"内容都还在盘上。

---

## 0. 一句话结论

保留 `Assets/Framework` 与其中的 **UI 组件库**；删除 **TJUIBuilder 全部产物** 和 **之前拼的背包 UI**；然后用组件库的通用 Prefab 当积木，**重新拼一个背包页**，页面只写一份业务脚本。

**当前进度（2026-10-09 晚）：第 0 步清理已执行完成并通过验证；下一步是第 1 步「组件库装配契约审计」。**

---

## 0.5 第 0 步执行记录（2026-10-09，已执行并验证）

### 已删除（清单 A/B，22 项全部成功）

| 目标 | 结果 |
| :--- | :--- |
| `Packages/manifest.json` 的 `cn.tuanjie.create.ui` 依赖行 | 已移除（`packages-lock.json` 同步清空） |
| `Assets/Games/`（+meta） | 已删 |
| `context/games/FrameworkExample/` | 已删 |
| `Assets/_ClassicRef/`（+meta，~55MB） | 已删 |
| `Assets/Concave/`（+meta，旧组件库备份） | 已删 |
| `Assets/__BrickProbe 1`、`__BrickProbe 2`（+meta） | 已删 |
| `.codely-cli/extensions/TJUIBuilder/`（扩展本体，用户拍板） | 已删 |
| `Assets/Example/Scripts/UI/InventoryPage.cs`、`InventoryGridItemView.cs`（+meta） | 已删 |
| `Assets/Example/Scripts/InventoryDemoEntry.cs`（+meta） | 已删 |
| 场景 `UITextEffectsDemo.unity` 的 `InventoryDemoEntry` 对象 | 已移除（现根对象：Camera / EventSystem / UGUIRoot / UITextEffectsDemoBootstrap） |
| `Assets/Framework/Docs/UIComponentLibraryExampleTutorial.md`（+meta） | 已删（47 处旧背包内容） |
| `Assets/Framework/Docs/UIComponentLibraryPlan.md`（+meta） | 已删（已执行完的规划文档） |
| `Assets/Framework/Docs/UIFrameworkExtensionTeaching.md` | **保留**（1260 行仅 1 处提及背包，属通用框架教学；用户裁定保留） |

> 注：§2.6（列三个文档待删）与 §2.7（声明 `Assets/Framework/` 含 Docs 不动）原本自相矛盾；已按内容实质核实后由用户裁定：只删与旧背包示例直接相关的两个文档。

### §2.4 组件库污染修复（已完成）

`Assets/Framework/UI/ComponentLibrary/Items/UIListItem - Grid Slot.prefab` 根组件由 `InventoryGridItemView` 还原为通用 `UIListItem`（GUID `aa28a246…` → `338979c4…`；`mSelectedIndicator`/`mHoverIndicator` 与基类字段完全一致，无损）。验证：根组件 = RectTransform / CanvasRenderer / Image / UIItemIcon / UIDragHandle / **UIListItem**，无 Missing Script。

### ⚠️ 执行中发现的三处交接文档遗漏（务必记录）

1. **清单 A 漏了 C# 编译级依赖（已修复）**：`Assets/Framework/Runtime/UI/Concave/` 的保留脚本在**编译期**依赖被删的 `Assets/Concave` 备份包内的基类/工具类；删除后 Unity 报 9 个 `CS0246/CS0103/CS0115` 错误。已从参考工程 `E:\unityXiangmu\UIDesign` 拷回 5 个文件到 `Assets/Framework/Runtime/UI/Concave/` 并统一命名空间为 `GameFramework.Concave`：`SelectionSlider.cs`、`NotificationType.cs`、`NotificationPositionType.cs`、`SlicedFilledImage.cs`、`Utils.cs`。修复后 0 错误。
   - **教训**：资产"引用已清零" ≠ C# 编译无依赖。清理共享目录前必须做一次编译验证。
2. **§2.8 结论修正**：Concave 原作者脚本**并非无引用**——12 个脚本全部被保留的组件库 Prefab 引用（`Popup Base` 用 FadeButton/FadeConfig，各 Switch 用 Switch/Gradient，Slider 用 SliderAmountText，进度条用 SliderAnimation/ProgressBarAmountText，`Notification - Concave (Outline)` 用 Notification，Checkbox 用 ToggleWithLabel 等）。**全部保留，未删。**
3. **历史遗留缺陷（未处理，待用户定夺）**：`Assets/Framework/Prefabs/UI/UGUIRoot.prefab` 根节点挂着一个 **Missing Script**（GUID `49949102e2054b339ffcc12be3fd374b`）。全盘搜索确认该 GUID 属于**旧项目** `E:\unityXiangmu\ShootingEditor2D\Assets\Scripts\ViewController\UI\UGUIRootBootstrap.cs`——是从 ShootingEditor2D 拷贝框架时带过来的残留组件，在 FrameworkExample 中从未存在。Prefab 内容定格于 2026-09-26，**与本次清理无关**；因属框架 Prefab（§2.7 声明不动）未擅自修改。

### 验证证据（2026-10-09）

- `unity_editor.refresh`：**0 errors / 0 warnings**
- `dotnet build Assembly-CSharp.csproj --no-restore`：**0 errors**（16 warnings 为既有依赖解析告警）
- 全工程搜索 `InventoryPage|InventoryGridItemView|InventoryDemoEntry|aa28a246…`：仅剩 `UIFrameworkExtensionTeaching.md` 的文字提及
- `Packages/packages-lock.json` 已无 `cn.tuanjie.create.ui`
- 工程文件（.csproj）曾因移除扩展后 TMP 由 3.0.9 回落 3.0.6 而过期，已重新生成（`SyncVS`）

---

## 1. 用户明确要求（2026-10-09）

1. 给框架做组件库的目的：**用组件库里的通用 Prefab 拼 UI，像搭积木**；不需要为拼页面额外写 UI 栈逻辑，**只需单独写一份"这个面板的具体功能逻辑"**。
2. 删掉 TJUIBuilder 相关的一切。
3. 删掉之前拼的背包 UI。
4. **不参考旧背包界面**，完全重做一个符合当前项目框架设计（UI 框架 + 整个 Framework）的背包页。
5. 必须使用组件库的组件；**允许更改 sprite**。
6. 先整理头绪，**不要直接开干**。

---

## 2. 已核实的事实（2026-10-09 只读盘查）

### 2.1 工程与场景

- Unity：`2021.3.44f1c1`
- 主场景：`Assets/Example/Scenes/UITextEffectsDemo.unity`
- 场景根对象：`Camera` / `EventSystem` / `UGUIRoot`（框架 Prefab 实例）/ `UITextEffectsDemoBootstrap` / `InventoryDemoEntry`

### 2.2 现存 bug：进 Play 必现"背包页面打开失败"

链条（已逐环核实）：

- `Assets/Example/Scripts/InventoryDemoEntry.cs` 在 `Start()` 里执行 `OpenAsync<Example.InventoryPage>(...)`
- `Assets/Example/Scripts/UI/InventoryPage.cs` 声明 `[UIPage("UI/InventoryPage", UIOpenMode.Single, UILayer.Normal)]`
- 因此需要 `Assets/Example/Resources/UI/InventoryPage.prefab`
- **该 Prefab 已不存在**（全工程 glob 无匹配；`Assets/Example/Resources/UI/` 下只剩 `UITextEffectsDemoPage.prefab`）

这就是"bug 还一堆"的直接来源之一。清理清单 B 会一并处理。

### 2.3 组件库（**要保留**）

- 位置：`Assets/Framework/UI/ComponentLibrary/`
- 分类目录：`Busy` / `Containers` / `Controls` / `Feedback` / `Foundations` / `Indicators` / `Internals` / `Items` / `Lists` / `Slots`，另有 `README.md`、`ComponentLibraryManifest.json`
- 对应运行时组件：`Assets/Framework/Runtime/UI/Components/`（`UIButton`、`UIBusyState`、`UIDragHandle`、`UIDropTarget`、`UIListItem`、`UIVirtualList`、`UISimpleList`、`UIProgressBar`、`UISlider`、`UIInputField`、`UITabGroup`、`UITabItem`、`UITooltip`、`UITooltipController`、`UIBadge`、`UIItemIcon`、`UIItemCount`、`UICooldownView`、`UIFocusController`、`UISelectableState`、`UIWindowAnimation`、`UIPopupPresenter`、`UIMessagePresenter`、`UILoadingIndicator`、`UIButtonState`、`TextEffect/`）
- 命名空间：`GameFramework`
- 字体：全量统一 `TikTokFront SDF`
- 注意：`ComponentLibraryManifest.json` 已过时（`sourceScene` 仍指向 Concave，部分条目名与现有文件不一致），**只作参考，不要当权威清单**。

### 2.4 ⚠️ 组件库被"示例脚本"污染（清理时必须**修复**而非删除）

- `Assets/Framework/UI/ComponentLibrary/Items/UIListItem - Grid Slot.prefab` 根节点上的 MonoBehaviour 引用 `guid: aa28a2460a64a2d469d53aa45c10e76a`
- 该 guid 对应 `Assets/Example/Scripts/UI/InventoryGridItemView.cs`（**Example 示例脚本**）
- 后果：直接删掉该脚本 → 这个库 Prefab 变成 **Missing Script**，组件库被破坏
- **正确做法：把根组件还原为通用 `UIListItem`**
- 约束提醒：用户 2026-10-05 明确"禁止修改 `Assets/Framework/UI/ComponentLibrary` 内的 Prefab"。此处属于**清理遗留污染的必要修复**，动手前需向用户确认。

### 2.5 删除清单 A —— TJUIBuilder 引入的产物

| 目标 | 内容 / 规模 |
| :--- | :--- |
| `Packages/manifest.json` | `"cn.tuanjie.create.ui": "file:../.codely-cli/extensions/TJUIBuilder/Packages/cn.tuanjie.create.ui"` 依赖行 |
| `Assets/Games/FrameworkExample/` | 77 个文件：`Prefabs/UIRoot.prefab`、`UI/PanelRegistry.asset`、`UI/Prefabs/InventoryPanel.prefab`、`UI/Prefabs/InventoryItemTemplate.prefab`、`UI/Images/*.png`（22 张）、`Scripts/InventoryPanel.cs`、`Scripts/UIGameInstaller.cs`、`Scripts/InventoryDemoService.cs`、`Scripts/FrameworkExamplePanelNames.cs`、`Editor/BuildInventoryPageFromLibrary.cs` |
| `context/games/FrameworkExample/` | 17 个文件：`UI需求.md`、`UI设计文档.md`、`image_requirements.json`、`ui-image-generator/`、`ui-panel-builder/`、`GameScripts/`、`EditorScripts/` |
| `Assets/_ClassicRef/` | ~55MB，UltimateCleanGUIPack Classic 参考资源，示例不依赖 |
| `Assets/Concave/` | 旧组件库备份，引用已清零，可安全删除 |
| `Assets/__BrickProbe 1`、`Assets/__BrickProbe 2` | 空目录（探针残留） |
| **待用户拍板**：`.codely-cli/extensions/TJUIBuilder/` | 扩展本体。删掉等于**彻底移除这套能力**（不是项目内容，是工具安装） |

### 2.6 删除清单 B —— 之前拼的背包 UI

- `Assets/Example/Scripts/UI/InventoryPage.cs`
- `Assets/Example/Scripts/UI/InventoryGridItemView.cs`
- `Assets/Example/Scripts/InventoryDemoEntry.cs` + 场景中的 `InventoryDemoEntry` 对象
- `Assets/Example/Resources/UI/InventoryPage.prefab`（**已不存在，无需删**）
- 文档中的背包章节：`Assets/Framework/Docs/UIComponentLibraryExampleTutorial.md`、`UIComponentLibraryPlan.md`、`UIFrameworkExtensionTeaching.md`

### 2.7 明确**不动**的内容

- `Assets/Framework/`（含 `Runtime/UI`、`Prefabs/UI/UGUIRoot.prefab`、`Docs/`）
- `Assets/Framework/UI/ComponentLibrary/`（唯一例外是 §2.4 的还原修复）
- `Assets/Example/` 的场景、文字特效示例、字体（`Front/TikTokFront.otf`）、`Resources/Audio`、`Resources/UI/ItemIcons`
- `Assets/Plugins/`、`Assets/TextMesh Pro/`

### 2.8 顺带发现（低优先级）

- `AI_HANDOFF.md` §3.4 提到的 `ExampleScenePage` / `ExamplePersistentPage` / `ExampleDetailPage` / `ExampleMultiplePage` **已不存在** → 该文档此节已过时
- `Assets/Framework/Runtime/UI/Concave/` 仍留着 Concave 原作者的脚本（`FadeButton`、`FadeConfig`、`Gradient`、`LoopableSelectionSlider`、`NonLoopableSelectionSlider`、`Notification`、`ProgressBarAmountText`、`SliderAmountText`、`SliderAnimation`、`SliderColorSwap`、`Switch`、`ToggleWithLabel`）→ 需确认是否仍有引用；若已无引用可一并清理

---

## 3. 难度归因（回答："是组件库不完善/未适配框架，还是 AI 做 uGUI 不行？"）

**结论：主因是 ①装配契约缺失 ②视觉不可自验。不是组件库不可用，也不是 AI 完全做不了 uGUI。**

1. **组件库层面**
   - 入口 Prefab 目前基本是"视觉壳"，**没有统一的对外配置接口**（要改什么、能改什么、改了会不会崩，全靠猜）。
   - **缺页面级骨架积木**（栅格 / 分区容器 / 自适应列表容器）→ 拼大页面时只能靠摆 RectTransform 数值，稳定性靠运气。

2. **框架适配层面**
   - 框架本体是清晰的：`UIPage` 生命周期 + `UGUIRoot` 五层 + `IUGUIService` 加载。
   - 但组件库 ↔ 框架的对接点是**口头约定**：`UIBusyState` 必须挂内容区 `Bottom` 并拉满锚点、`UIDropTarget` 必须手填 `TargetTag`、`UIVirtualList` 必须喂 `itemTemplate`……**没有任何强制或校验**。摆错**不报错，只是看起来不对**。

3. **AI 做 uGUI 的固有短板**
   - uGUI 是像素级布局：锚点 / 层级 / 数值的组合错误**绝大多数不产生异常**，只产生"丑"。
   - 而本环境的视觉验收不可靠（曾因预算超限不可用，且用户已否定过"AI 自称视觉验收"）→ AI 只能保证**"结构正确、脚本能跑"**，保证不了**"好看"**。
   - 之前的执行错误正是：把前者当成了后者。

---

## 4. 已否决 / 已废弃方案（保留记录，避免重走）

- **Root 级共享 PopupBlocker**：已否决（`UILayer` 被错当成输入策略；共享遮罩层级可能盖住页面自身按钮）。输入遮挡改为**页面级**。
- **UIElementNavigator 局部导航器**：仅讨论过，未实现，不进运行时代码。
- **UGUI 与 UI Toolkit 立即合并成一套完整抽象**：暂缓。
- **把对象池直接塞进 UGUIService**：暂缓（普通页面与高频伤害跳字生命周期不同）。
- **直接接入 YooAsset / HybridCLR**：暂缓，核心资源入口保持 `IAssetLoader`。
- **示例页 1500×800 照搬官方 demo 排版**：用户否决（"丑哭了"）。
- **参考官方 Classic demo 的 Layout 照搬**：用户否决，**只取视觉语言**。
- **TJUIBuilder 的 `Create.GameFramework` / `UIBrick` 栈与本项目框架并存**：用户否决（生成了另一套控件树，不是在用组件库 Prefab）。

---

## 5. 下一步计划（第 0 步已完成；第 1 步起待用户确认）

| 步骤 | 内容 | 产出 |
| :--- | :--- | :--- |
| 0 | ✅ **已完成（2026-10-09）**：执行清单 A/B；修复 §2.4 组件库污染；补回清单 A 遗漏的 Concave 编译依赖 | 场景回到干净状态（详见 §0.5） |
| 1 | **组件库审计**：不写代码，逐 Prefab 输出《装配契约》 | 每个通用 Prefab 一张卡：入口 / 可嵌套方式 / 必填配置 / 默认状态 / 与框架生命周期关系 / 已知限制。**这是让"搭积木"成立的中间层。** |
| 2 | **定背包页信息结构**（完全不看旧页） | 只从契约里挑积木；只有契约确实缺的，才讨论补一个**通用**组件（不给背包特供） |
| 3 | **只写一份页面业务脚本** | 装配全部由积木完成 |
| 4 | **验收** | 结构 / 运行时数据由 AI 出证据；**画面由用户过目**（截图 / 录像），AI 不自称"已视觉验收" |

---

## 6. 环境与验证注意

- **Play Mode 冻结**：Unity/Tuanjie 编辑器窗口失焦或最小化时，Play Mode 会完全冻结（`isFocused=False`，Run In Background 无效）。动态验证与 Game View 录像**必须保持 Unity 窗口前台焦点**，否则录到的是静止帧。
- 修改 `.cs` / `.unity` / `.prefab` / `.asset` **前必须确认编辑器不在 Play 模式**。
- 编译检查：`dotnet build E:\unityXiangmu\FrameworkExample\Assembly-CSharp.csproj --no-restore`
- `RoslynValidator` 需在含 `.com-unity-codely.json` 的目录运行，否则误报。
- 全局 `GameAssembly` 一旦报错，会**连带 Create 包报 "Could not resolve field token" 假错误**；修好自研代码即消失。
- 文字特效调参可走 `Assets/Example/Editor/UITextEffectPreviewTool.cs`（Edit Mode 逐帧渲染帧条 PNG），不受 Play Mode 冻结影响。

---

## 7. 关键约束速查

- **命名空间陷阱**：项目自研 = `GameFramework`；TJUIBuilder = `Create.GameFramework`。**同名不同命名空间**，混用会 `CS0104`。面板脚本需 `using ProjectUI = GameFramework;` 并全限定引用。
- **框架 UI 三层分工**：`UGUIService`（资源 / 实例 / 生命周期 / Handle 释放）/ `UINavigationService`（前进 / 返回 / 历史）/ `UIPageAttribute`（页面类型级配置）。
- **UIPage 生命周期**：`OnOpen` → `OnShow` → `OnHide` → `OnClose`。
- **UGUIRoot 五层**：`BackgroundLayer` / `NormalLayer` / `PopupLayer` / `SystemLayer` / `ToastLayer`。
- **`UILayer` 只表示显示层级**，不自动推断输入遮挡、返回栈或页面生命周期。
- **字体**：`TikTokFront SDF`。
- **用户授权边界**：可直接改 `Assets/Example` 下的场景、示例代码、示例 Prefab；**不得改 `Assets/Framework/UI/ComponentLibrary` 内的 Prefab**（§2.4 的修复需先确认）。

---

## 8. 交接后的工作方式（沿用 `AGENTS.md`）

1. 先读本文件 + `AI_HANDOFF.md` + `AGENTS.md`。
2. 区分：**当前代码事实 / 已验证结果 / 外部资料结论 / 仅讨论过的方案 / 待确认事项**。
3. 对历史教学文档只取设计背景，**不把旧代码当当前代码**。
4. 实现新模块前，先给"当前问题 → 成熟方案对照 → 采用 / 暂缓理由 → 最小修改范围"。
5. 用户未明确要求改代码时，只写方案或文档。
6. 每次只推进一个可验证的垂直功能，完成后记录验证命令、结果、以及**仍未验证的部分**。
7. 视觉产物一律交用户确认，**不得自称已视觉验收**。

## 2026-10-10 第一轮实施结果

已完成一条本地装配闭环（源码与 Prefab 已修改）。

- `UISimpleList`、`UIVirtualList` 增加 `ItemBound`，隐藏状态初始化改为幂等；两个列表模板 fileID 修正为当前条目组件。
- 新增 `UIItemView`，统一填充标题、图标、数量、锁定和品质表现；列表格子连接该视图并关闭首轮拖拽。
- 新增 `InventoryDemoPage`、`InventoryDemoModel`、`LocalInventoryDemoSystem` 和入口场景 `InventoryAssemblyDemo`。页面只处理映射、选择、过滤和业务意图，System 更新 Model。
- 新增 `UIComponentLibraryValidator`，用 `PrefabUtility` 检查 Missing Script、列表模板、ScrollRect/Content、虚拟布局冲突和数量节点。
- Unity 编译 0 错误、0 警告；Play Mode 已验证打开、48 条数据、稳定 ID 操作、关闭重开、过滤 31 条和截图。
- 模拟联机仍暂缓；以后在同一套页面接口上替换 System，不依赖 `E:\MMORPG_NaYiNa_Fantasy`。

验证与回退材料：`C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI第一轮`。

## 2026-10-10 视觉修订

用户指出第一版视觉效果差且没有真正使用组件库。已重新装配 `InventoryDemoPage.prefab`：外壳使用 `Popup Base - Concave`，关闭入口使用外壳内置按钮，列表继续使用 `UIVirtualList - Grid View`，操作按钮使用 `Button - Filled With Border`，条目图标改用组件库的 PotionIcon/Gem/ShieldIcon/SwordIcon，锁定覆盖使用组件库 Lock.png。删除上一版临时生成的图标 PNG。Unity 编译 0 错误、0 警告；运行时验证条目选择与详情更新，内置关闭按钮关闭页面。视觉最终是否符合项目审美由用户确认。

视觉截图与回退材料：`C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI视觉修订`。

## 2026-10-11 组件库装配审计与输入框契约修复

本次按整个 UI 组件库扫描：72 个 Prefab（57 普通、15 Variant），未发现 Missing Script。15 个 Variant 中有 10 个相对父 Prefab 增加了框架组件，因此纯视觉变体与能力变体需要分别写明装配契约。完整清单、差异和设计判断见 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI组件库装配审计\装配结论.md`。

完成一个输入框垂直切片：`UIInputField` 在未显式配置时可绑定唯一子 TMP_InputField；`Input List Group (With Icons)` 的三个实际输入框各有独立包装组件。Play Mode 读写三个字段与多行输入框通过，构建 0 错误（8 个现有 ValueTuple 警告）。修改文件、差异、哈希、验证记录和已演练回退脚本均在上述审计目录。

下一步优先验证“组件音效事件统一接线”是否能在背包与商店共用，以减少页面级重复订阅，并检查点击打开详情页的可选组件契约。页面业务仍负责数据映射和购买/装备意图；网络请求边界仍在业务 System。

## 2026-10-11 UI 音效统一装配

完成通用 `UIAudioBinder`：页面根节点配置音频组，`UGUIService` 注入 `IAudioService`；统一订阅按钮、列表项、页签、输入、滑块、拖放、弹窗、通知等控件的 `SoundRequested`。虚拟/普通列表在 `ItemBound` 时重新订阅复用条目。页面关闭后解除列表事件并卸载音频组。背包页原有逐个订阅与音频加载/卸载代码已删除；背包和商店 Prefab 及其条目模板配置 Cue。

审计发现 `AudioCueKey` 原来缺少 `[Serializable]`，导致 Prefab 无法保存 Cue 字段；已修复。`UITabGroup` 的默认选中不再触发用户音效。构建 0 错误、8 个现有 `System.ValueTuple` 警告。Play Mode 验证背包 1200 条虚拟列表和商店 36 条商品的实际条目点击都使 AudioSource 播放 `Genshin Menu Collapse`；背包按钮触发 `Genshin Menu Expand`。后续补验收确认内层搜索框触发声音、页面隐藏/显示分别播放关闭/打开音、列表条目重绑后仍能播放。异步音频组在页面隐藏期间加载完成不会补播旧声音，再显示时播放一次。仍未对整个组件库逐个做声音和听感验收。

修改前后文件、差异、哈希、验证记录及已演练回退脚本：`C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI音效统一装配`。

## 2026-10-11 UI 装配契约与校验器扩展

本轮按整个库核对输入、页签、滑块等装配要求；在现有 `UIComponentLibraryValidator` 中新增输入目标歧义、页签空项和内容面板数量错配检查。72 个库 Prefab 运行校验为 0 错误；临时反例 Prefab 分别触发预期诊断，测试后删除；Editor 工程构建 0 错误、9 个现有依赖版本警告。契约矩阵、全量 Prefab/Variant 清单、源码差异、验证记录和已演练回退脚本：`C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI装配契约`。

确认一个未修复的能力缺口：4 个 Selection Slider 是左右按钮切选项，均没有 Unity `Slider`，其挂载的 `UISlider` 包装组件没有数值目标；暂不把它们当作原生滑块或纳入硬错误。下一条垂直切片应核对原 Concave 选项脚本的接口和引用，再决定如何给它们提供符合框架的选项切换能力。通用“点击打开详情页”仍仅在方案阶段，未实现。

## 2026-10-11 Selection Slider 框架化

上一段记录的 4 个 Selection Slider 空 `UISlider` 问题已在本轮解决。新增 `UIOptionSelector`，迁移 4 个 Prefab 的 3 个原选项、左右按钮与循环/非循环规则；移除 Prefab 上的原 Concave 选择脚本、无目标 `UISlider` 和旧按钮持久化调用。原脚本源码暂保留，避免影响其他引用；这四个 Prefab 已不依赖它。新组件对外提供 Index/Value/Options、Changed 事件及 SetOptions/Select。校验器增加最低装配检查。

Play Mode 验证三个循环型切换 3 次回到第 1 项，非循环型到末项停止；事件次数分别符合预期。全库 72 Prefab 校验 0 错误；Editor 工程构建 0 错误（9 个现有依赖警告）。视觉/真实鼠标听感仍未单独验收。变更、验证和回退材料：`C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI选项切换组件`。下一步可做“可选点击打开详情页”这一类通用行为，但先检查现有页面数据传递契约。

## 2026-10-11 商店详情页装配

在现有 `UIVirtualList.ItemClicked` 和 `IUGUIService.OpenAsync<TPage,TData>` 上接通商店卡片点击：`ShopDemoPage` 只负责把商品映射为强类型 `ShopDetailData`；`ShopDetailPage` 只显示数据和关闭自身。没有新增仅转发事件的框架组件，也没有修改组件库 Prefab。

`Assets/Example/Resources/UI/ShopDetailPage.prefab` 已在 Unity 编辑器内由 `ShopDetailPrefabBuilder` 实际生成，包含组件库 `Popup Base - Concave` 的嵌套 Prefab 实例、商品图文、关闭按钮与 `UIAudioBinder`。编辑器检查四个页面引用均非空；Editor 工程构建 0 错误、9 个现有警告。Play Mode 验证商店打开、列表点击“生命药剂 1”后详情标题匹配、原生关闭按钮关闭详情页，最后已退出 Play 模式。真实鼠标命中、最终视觉和听感尚未验收。此前一度只写了装配脚本却没有生成 Prefab，已在本轮纠正并验证文件存在。

## 2026-10-11 商店详情视觉与层级修正

实际 Game View 录制发现详情外壳在 `800×600` 参考分辨率的 Canvas 下放大到接近全屏，且场景入口按钮作为 UGUIRoot 尾部子节点压在 PopupLayer 前。`ShopDetailPrefabBuilder` 将嵌套 Concave 弹窗缩放为 `0.6`；`InventoryAssemblyDemo.unity` 将两个场景入口移到 BackgroundLayer，使弹窗和遮罩盖住入口。关闭按钮去掉与页面级关闭音重复的 Cue。

Unity 编译 0 错误、0 警告；Editor 工程构建 0 错误、9 个既有依赖警告。Play Mode 录制并检查 `screenshots/ShopDetailVisualFinal.png`：弹窗居中，商店可见于暗化背景，标题、图标、价格、库存不重叠。射线检查关闭图标命中 CloseButton 子节点，弹窗外命中 Backdrop。原生 Button.onClick 后详情实例销毁，AudioRoot 中播放 `Genshin Menu Close`；仍未用物理鼠标与耳朵确认点击手感和音量。Play Mode 已退出。修改前场景和 Prefab 副本位于 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\商店详情验收`。

## 2026-10-11 商店关闭按钮射线修复

用户反馈商店页右上角关闭按钮点击无效。Play Mode 射线检查确认按钮中心第一命中是余额 `Summary` TMP_Text，关闭按钮内 `Icon` 排第二；按钮本身启用且可交互，页面 `Close` 订阅正常。只在 `Assets/Example/Resources/UI/ShopDemoPage.prefab` 将 `Summary` 的 `Raycast Target` 设为 false。修正后同一点第一命中 `Icon parent=CloseButton`，EventSystem 点击分发后商店实例消失。Unity 编译 0 错误、0 警告；Play Mode 已退出。修改前副本、补丁、验证和回退位于 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\商店关闭按钮修复`。真实鼠标点击仍待用户/桌面实测。

## 2026-10-11 组件库装配边界复核（文档）

用户确认无需继续做相似的示例 UI。本轮只复核现有背包/商店页面、列表条目、音效绑定、Busy 和强类型详情页接口，并将具体装配步骤及缺口优先级补到 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI装配契约\README.md`。未修改源码/Prefab，也未做新的 Unity 运行态验证。

当前可用：列表可见项复用、条目展示、可选拖放、点击详情的页面级强类型导航、统一音效绑定。页面仍负责业务映射和意图，这是有意保留的边界；通用“点击打开详情”组件尚未实现，当前只有商店一处实际需求。15 个 Variant 中 10 个增加框架能力组件，不能把所有 Variant 都当纯换皮。

联机前应优先处理商店请求生命周期：`ShopDemoPage` 的 pending 只在购买回调复位，Busy 超时只解除遮罩；本地 System 同步回调不能验证异步超时、关闭后迟到结果。下一条实施切片应先补这一条真实边界并做可控延迟测试，不依赖旧 MMORPG 项目。

## 2026-10-11 商店异步请求页面收尾

上一段的页面级超时缺口已处理：`ShopDemoPage` 订阅 `UIBusyState.TimeoutTriggered`；超时后结束 pending、标记购买结果未确认并保持购买按钮禁用，避免把超时误判为交易失败而重复购买。原请求迟到结果仍可结算并恢复按钮。页面关闭时解除订阅并清理 Busy，关闭后的回调被忽略。未修改框架组件或 Prefab。

`Assembly-CSharp.csproj` 构建 0 错误、8 个既有依赖警告；Unity 刷新 0 错误、0 警告。Play Mode 反射探针验证超时、迟到回调和关闭后回调，结束时已退出 Play Mode。源码副本、补丁、精确输出和已在临时目标上演练的回退脚本见 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\商店异步请求收尾`。这是可控延迟的页面级验证，尚无真实网络 System；服务端幂等标识/结果查询及重连行为仍待实现。

原有本地同步购买另做回归：金币 `500→475`、第 1 个商品库存 `2→1`，没有被超时收尾逻辑影响。Play Mode 再次退出。

## 2026-10-11 红色按钮 Variant 继承修复

对 15 个 Variant 逐项读取父级、按钮数量、动画控制器引用和关键行为字段覆盖，发现 `Button - Filled With Border - Red` 的 `Animator.m_Controller` 覆盖与父版当前同值。它会阻断未来父版控制器更新的继承。仅从红色 Variant 删除该 6 行冗余覆盖，颜色、文案和 CanvasGroup 均保留；未改父 Prefab 或其他变体。

Unity 刷新 0 错误/0 警告，变体探针确认红色按钮 `sameController=True behaviorOverrides=`，全库 72 个 Prefab 校验 0 错误。没有做 Play Mode 动画视觉验收。原件、修改件、补丁、验证与回退见 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI红色按钮Variant继承`。

后续 Play Mode 对照父版、蓝色和红色按钮：EventSystem 指针点击各触发一次 `UIButton.Clicked`；悬停/按压 Animator 状态和 CanvasGroup alpha 一致（按压 alpha 均为 `0.700`）。已退出 Play Mode。该结果验证交互与动画状态机，真实鼠标射线命中和最终视觉手感仍未验收。

## 2026-10-11 背包/商店装配成本审计

只读编辑器探针确认：背包页面 17 个序列化字段、11 个组件库嵌套实例；商店 10 个字段、7 个嵌套实例。两页均用组件库外壳、列表、按钮、页签、输入、Busy、滚动条和根级 `UIAudioBinder`。稳定 ID 恢复列表选中的约 5 行在两页重复，但目前不值得为两处新增通用 API。业务映射、筛选、购买/装备意图和结果解释继续留在页面/System。

发现一个已运行态复现的装配缺口：商店独立条目 `ShopItemCard.prefab` 自带启用的 `UIDragHandle`，却没有载荷或拖拽图标。Play Mode 可见商品卡拖拽时 `moved=True`、结束后 `restored=True`。通用 `UIListItem - Grid Slot` 也默认带拖拽，并被两种库列表用作默认模板。下一条垂直切片应让展示条目默认不拖、背包显式选择拖拽能力、商店去掉无意义拖拽；不同时新增页面或选中状态抽象。详细清单与验证边界：`C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI装配成本审计\结论.md`。本轮未改项目源码/Prefab，Play Mode 已退出。

## 2026-10-11 条目拖拽改为显式能力

上一段提出的装配缺口已处理。通用 `UIListItem - Grid Slot` 移除默认 `UIDragHandle`；新增真正继承它的 `UIListItem - Draggable Grid Slot` Prefab Variant，由背包页列表显式选用；商店 `ShopItemCard` 移除无载荷拖拽。展示/点击仍留在基础条目，拖放能力只出现在选择该 Variant 的页面；未改运行时 C# 或其他页面。

Unity 刷新 0 错误、0 警告，全库 73 Prefab 校验 0 错误。Play Mode 验证背包 1200 条仅实例化 30 个可见项且 30 个都有拖拽；原有有效/无效装备拖放探针通过。商店 36 条仅实例化 24 个，拖拽组件 0 个，点击事件触发一次且 AudioSource 播放 `Genshin Menu Collapse`；滚动条把可见索引 `0–23` 移到 `16–35`，实例数不变。已退出 Play Mode。真实鼠标与视觉/听感仍待人工验收。修改件、差异、精确验证记录和已隔离演练的回滚脚本位于 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI条目可选拖拽`。

## 2026-10-11 Selection Slider 点击音效

检查四种 `Selection Slider` Prefab，发现 `UIOptionSelector` 左右按钮的 `UIButton.mClickSound` 全为空；原有 `UIAudioBinder` 已统一接入按钮音效请求。只给八个按钮配置已有 `Audio/CommonUI/MenuCollapse` Cue，没有新增音频代码或改变动画、选项逻辑。四个 Prefab 各仅改变四行序列化值。

Unity 刷新 0 错误/0 警告，全库 73 Prefab 校验 0 错误。Play Mode 中实例化背景型变体并附加现有 `UIAudioBinder`，点击 Next 后选项 `0→1`，`AudioSource` 播放 `Genshin Menu Collapse`。四种循环/非循环切换规则再次通过；已退出 Play Mode。未人工验收真实听感和鼠标命中。原件、修改件、差异、验证与已隔离演练的回滚脚本：`C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI选项切换音效`。

## 2026-10-11 列表条目可选拖拽回收

背包 Play Mode 复现：虚拟列表 `Clear()` 后，条目已隐藏、索引为 `-1`，但其 `UIDragHandle.Payload` 仍是旧物品。`UISimpleList` 和 `UIVirtualList` 都调用 `UIListItem.ResetForPool()`，因此只在这个统一回收入口增加对可选 `UIDragHandle.ResetForPool()` 的调用，没有改列表或业务代码。

修改后 Editor 工程构建 0 错误、9 个既有依赖警告；Unity 刷新 0 错误、0 警告。Play Mode 验证旧载荷回收时清空、复用后重绑，1200 条数据仍只实例化 30 个条目；原有装备拖放回归通过，已退出 Play Mode。普通列表未单独建立运行态页面，但它与虚拟列表调用同一个回收入口。原件、差异、命令和隔离演练回滚见 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI列表拖拽回收`。

## 2026-10-11 输入框程序赋值不触发用户输入

复现发现 `UIInputField.SetText()` 使用 TMP `.text` 赋值，会触发打字音效，但不会发送业务变更；同时可能留下旧的搜索防抖协程。现在使用 `SetTextWithoutNotify()` 并取消旧防抖。用户真实输入仍触发一次音效和一次 `ValueChanged`，`Clear()` 仍发送一次空值事件。

Editor 构建 0 错误、9 个既有依赖警告；Unity 刷新 0 错误/0 警告。Play Mode 验证程序赋值 `sounds=0 changes=0`、用户输入 `sounds=1 changes=1`、清空后变更计数为 2，旧防抖没有迟到事件；已退出 Play Mode。原件、差异、精确探针和已隔离演练回滚：`C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI输入框程序赋值`。

## 2026-10-11 页签音效回退条件

`UITabGroup.mDefaultSelectSound` 的说明要求仅在目标页签没有自有音效时播放。Play Mode 临时给商店页签设置 Cue 后，单次点击实测组和页签各发一次请求。`UITabItem` 增加只读 `HasSelectSound`，组仅在其为 false 时请求组 Cue；未改 Prefab。修改后运行态分别验证无自有 Cue 时组请求 1 次、有自有 Cue 时页签请求 1 次、程序化切换静音。Editor 构建 0 错误、9 个既有依赖警告；Unity 刷新 0 错误/0 警告；已退出 Play Mode。原件、差异、精确命令和已隔离演练回滚见 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI页签音效回退`。

## 2026-10-11 已装配页面校验入口

为服务“Prefab 拼完就用”的主线，现有组件库校验器增加 `GameFramework/UI/Validate Assembled Pages`，扫描 `Assets` 内根节点带 `UIPage` 的实际页面，复用已有公共装配检查；不增加防御性规则或页面业务反射校验。四个当前页面 0 错误；临时制造缺失 `mItemTemplate` 的页面时准确报 1 错误，临时 Prefab 与 `.meta` 已清理。Editor 构建 0 错误、9 个既有依赖警告；Unity 刷新 0 错误/0 警告。此工具不验业务字段、视觉与射线命中。原件、差异、验证和已隔离演练的回滚脚本见 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\UI页面装配校验`。

## 2026-10-11 组件库入口装配说明

重写 `Assets/Framework/UI/ComponentLibrary/README.md` 的旧 Classic 主题简介，按页面外壳、按钮、页签、输入、列表、可选拖放、Busy、选项切换、反馈等实际需求列出 Prefab 入口与页面接线责任。明确 73 个当前 Prefab、来源 Manifest 仅属历史映射、普通条目与拖拽 Variant 的能力差异，以及单机/联机请求均由业务 System 承担。此轮只改文档，未修改运行时代码或 Prefab，也未做新的 Play Mode 验证。后续以该入口文档和当前源码为准，不用旧 72 件审计快照推断现状。

## 2026-10-11 商店组件独立装配试做

在 `Assets/Example/AssemblyTrials/ShopAssemblyTrial.prefab` 中从组件库直接装配商店骨架：弹窗壳、页签、搜索、虚拟列表、滚动条、按钮和 Busy 共 7 个库 Prefab，商品卡继续使用业务专用 `ShopItemCard`。复用现有 `ShopDemoPage` 的 9 个业务引用，不修改正式商店页。Unity 静态验证结果为 `errors=0`、9 个字段无缺失、4 个页签、列表模板为 `ShopItemCard` 且无拖拽；组件来源映射已记录。隔离 Play Mode 实例化后显示 36 件商品，切装备页签后 12 件；已退出 Play Mode。测试未经过正式资源路径、购买流程及鼠标/视觉/听感验收。

这次实测确认：组件库已经能复用通用交互和动画，但页面布局、业务卡片、页签数量及字段连接仍需要显式装配。“拼完就用”目前可达到功能骨架复用，尚不能省掉所有 Inspector 装配。没有为一个页面新增运行时抽象；下一步先用第二种页面类型验证字段绑定是否重复，再决定是否做小型编辑器装配助手。详细记录、构建/验证脚本和回退材料在 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\商店组件装配试做`。

补做隔离 Play Mode 交互：TMP 输入值变更经防抖将 36 件筛至 12 件，条目点击选中并打开详情，详情关闭成功，本地购买让金币 `500→475`、商品库存 `2→1`。测试页实例销毁，Play Mode 已退出。该结果验证组件事件到商店业务代码的链路；尚未通过正式 `UGUIService` 路径或真实鼠标、视觉与听感验收。

截图检查又发现组件库基础按钮的 `UIButton.mLabel` 未绑定自身 `Text` 子节点，导致页面调用 `SetText` 仍显示默认文案。已只补该 Prefab 引用，并验证 Blue/Red Variant 继承后都显示“购买”；未修改按钮脚本、动画或音效。试做页调整了余额和列表间距，最终截图在 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\商店组件装配试做\ShopAssemblyTrial.png`。按钮改动和回退脚本同在该目录。静态校验仍为 0 错误，Play Mode 已退出。

## 2026-10-11 通用组件契约审计

按通用组件边界审计 73 个库 Prefab 和当前 5 个页面，新增校验器规则：`UIButton` 有唯一文字子节点却未绑定 `mLabel` 时报告错误。此前发现的基础按钮文案漏配已修复，Blue/Red Variant 验证通过；多行输入框的唯一 TMP 输入目标通过运行态自动绑定验证，保持简洁配置。现有库与页面均 0 错误；临时缺失按钮文字引用的反例准确报告 1 条后清理。Unity 编译 0 错误、0 警告。详细材料在 `C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\组件契约审计`。本轮没有新增一键装配工具或业务专属规则。

## 2026-10-11 底栏 Prefab 契约修复

审计弹窗/反馈组件时发现 `Bottom Bar - Concave` 这个纯底栏 Variant 误挂全空的 `UIPopupPresenter`。只移除该展示器，保留 `Bottom Bar` 父 Prefab 继承及 `UIWindowAnimation`；完整弹窗与 Notification 不改。审计记录由 7 个展示器降至 6 个，底栏验证输出 `source=Bottom Bar presenter=False animation=True`。73 个库 Prefab 和 5 个页面校验 0 错误，Unity 编译 0 错误/0 警告。材料与回退：`C:\Users\Admin\Documents\Codex\2026-10-10\zh\outputs\底栏Prefab契约修复`。

方向纠正：用户明确无需一键拼背包/商店的编辑器工具。后续只围绕几乎所有页面可复用的 Prefab 契约、可选能力、Variant 继承和通用校验推进；背包/商店只是验收样本，旧段落中的“编辑器装配助手”不再是计划。
