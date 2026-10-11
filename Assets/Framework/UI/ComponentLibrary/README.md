# UI 组件库：页面装配入口

本目录提供可嵌入页面的 UGUI Prefab。当前有 73 个 Prefab，包含结构/功能变体及配色变体。页面仍需自己的 `UIPage` 脚本处理数据映射、筛选、业务意图和结果；通用交互、动画和音效请求尽量留在组件内。

## 从需求选 Prefab

以下路径均相对于本目录。先拖入 Prefab 实例，保留其内部组件及引用，再在页面根节点引用需要交互的组件。

| 需求 | 入口 Prefab | 页面需接入 |
| :--- | :--- | :--- |
| 页面/弹窗外壳 | `Containers/Popups/Popup Base.prefab` 或 `Popup Base - Concave.prefab` | 页面继承 `UIPage`；关闭按钮接页面关闭命令。`Popup - Size Fitter` 等带 `UIPopupPresenter` 的完整弹窗按其 `Show/Close` 契约使用；`Bottom Bar` 及其 Concave Variant 只是底栏。 |
| 按钮命令 | `Controls/Buttons/Button - Filled With Border.prefab` | 订阅 `UIButton.Clicked`。Blue/Red 是该按钮的 Prefab Variant。 |
| 页签 | `Controls/Toggles/` 中带 `UITabGroup` / `UITabItem` 的组 Prefab | `TabChanged` 处理筛选；如需自动切内容，配置与页签一一对应的 `Content Panels`。普通 Toggle 不自动成为页签。 |
| 搜索/文本输入 | `Controls/InputFields/InputField - Single.prefab`，多行需求选 `Input Field - Multiline With Scrollbar.prefab` | 订阅 `UIInputField.ValueChanged` 或 `Submitted`；程序赋值用 `SetText`，清空用 `Clear`。组合输入框中的各字段需分别引用。 |
| 少量列表 | `Lists/UISimpleList - Vertical View.prefab` | `SetData` 后在 `ItemBound` 填充展示，`ItemClicked` 处理动作。 |
| 大量数据/网格 | `Lists/UIVirtualList - Grid View.prefab` | 同上；设置 ItemTemplate、尺寸和列数，滚动只实例化可见项。不要在其 Content 上另加布局组件。 |
| 普通条目 | `Items/UIListItem - Grid Slot.prefab` | 展示、点击、选中；默认无拖拽。 |
| 可拖条目/接收槽 | `Items/UIListItem - Draggable Grid Slot.prefab`、`Slots/UIDropTarget - Equip Slot.prefab` | 前者是真正继承普通条目的 Variant；在 `ItemBound` 设置 `UIDragHandle` 的 Payload/DragIcon，接收槽的 `Dropped` 中判定业务并反馈拖放结果。无拖拽需求继续用普通条目。 |
| 等待/忙碌 | `Busy/UIBusyState - Overlay.prefab` | 请求开始/结束时调用 `BeginBusy/EndBusy`；超时只解除遮罩，交易结果仍需业务层确认。 |
| 选项切换 | `Controls/SelectionSliders/` 下四种 Selection Slider | 这是左右按钮切换字符串选项的 `UIOptionSelector`，不是数值 `UISlider`；使用 `SetOptions`、`Changed`。 |
| 数值、进度和滚动 | `Controls/Sliders/`、`Indicators/ProgressBars/`、`Controls/Scrollbars/` | 按数值控件及 ScrollRect 的实际引用配置；Scrollbar Prefab 只提供滚动条，不能代替列表容器。 |
| 消息/提示 | `Feedback/Notifications/`、`Feedback/Tooltips/` | Notification 使用 `UIMessagePresenter`；提示方向 Variant 按所需布局选择。 |

`Controls/` 还提供 Dropdowns、Checkboxes、Switches；`Foundations/` 提供背景。`Internals/` 是被组件引用的贴图、字体、动画和音效资源，不是页面入口，也不要单独删除。字体入口目前使用 `TikTokFront SDF`。

## 组合成页面

1. 页面根节点挂具体 `UIPage` 和 `UIAudioBinder`，页面类型使用 `UIPageAttribute` 配置资源路径、层级和生命周期。音频组及 Cue 配在 Binder/控件上；运行时由 `UGUIService` 注入 `IAudioService`。未注册音频服务或未加载音频组时，Prefab 本身不会发声。
2. 在页面上引用已嵌套的列表、页签、输入框、按钮、Busy 等组件。列表数据用 `SetData`，复用条目每次 `ItemBound` 都要重填展示和可选行为数据。筛选后若要保留选择，业务页按稳定 ID 恢复，不能依赖旧索引。
3. 点击打开详情可以由页面订阅 `ItemClicked`，映射强类型页面数据并调用现有导航/API；当前没有专用的通用“点击打开详情”组件。拖放同样只提供交互事件，装备/购买等判定留给业务 System。
4. 单机与联机页面共用这些 Prefab。页面只表达请求、等待与结果；本地或网络请求实现在业务 System。Busy 超时不等于服务器拒绝或交易失败。

现有装配实例：`Assets/Example/Resources/UI/InventoryDemoPage.prefab`、`ShopDemoPage.prefab`、`ShopDetailPage.prefab`。商品卡 `Assets/Example/Resources/UI/ShopItemCard.prefab` 是结构不同的业务 Prefab，不属于 `UIListItem - Grid Slot` 的换色 Variant。

## Variant 与校验

- 只改颜色/形状且逻辑一致的变体应继承主 Prefab，避免复制交互和动画配置。增加拖拽等能力的变体也可以继承，但要写清新增能力；结构或行为不一致时使用独立 Prefab。
- `ComponentLibraryManifest.json` 记录早期资源来源映射，不是当前 73 个 Prefab 的完整清单，也不能据此判断当前装配能力。
- 在 Unity 编辑器执行 `GameFramework/UI/Validate Component Library` 检查库资源；页面拼完后执行 `GameFramework/UI/Validate Assembled Pages` 检查根节点带 `UIPage` 的 Prefab。校验不覆盖页面业务字段、视觉、射线命中或听感，这些仍需在 Play Mode 验收。
