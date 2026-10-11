# FrameworkExample UI 组件库使用说明

## 一、组件库设计原则与架构定位

本项目 UI 组件库基于四层架构设计，位于 `Assets/Framework/Runtime/UI/Components/`。

```text
┌────────────────────────────────────────────────────────┐
│ 1. 表现组件层 (UI Components: UITabGroup, UIButton...) │ ◄── 本组件库
└───────────────────────────┬────────────────────────────┘
                            │ 监听交互事件 / 数据绑定
┌───────────────────────────▼────────────────────────────┐
│ 2. 页面控制器层 (UIPage / Controller)                   │
└───────────────────────────┬────────────────────────────┘
                            │ 发送 Command / 监听领域事件
┌───────────────────────────▼────────────────────────────┐
│ 3. 业务系统层 (System: InventorySystem, QuestSystem...)  │
└───────────────────────────┬────────────────────────────┘
                            │ 读写数据
┌───────────────────────────▼────────────────────────────┐
│ 4. 数据模型层 (Model: ItemModel, PlayerModel...)        │
└────────────────────────────────────────────────────────┘
```

### 核心准则
1. **纯表现与交互**：组件只负责 UI 渲染、动效和输入捕捉，**严禁直接持有业务 Model 或直接调用网络接口**。
2. **解耦音频服务**：所有交互组件均抛出 `SoundRequested(AudioCueKey)` 事件，由页面或专门的音频适配器调用 `IAudioService.Play(key)`，组件内部不强依赖音频单例。
3. **对象池标准重置**：每个组件均提供 `ResetForPool()` 方法，彻底清理注册事件、协程与瞬态数据，防止对象池复用时状态残留。
4. **统一字体规范**：所有涉及文本渲染的组件及 Concave Prefab 统一使用 `Assets/Example/Front/TikTokFront SDF.asset`。

---

## 二、组件清单与使用说明

### 【P0 页面与异步请求基础】

#### 1. `UITabGroup` 与 `UITabItem`
- **定位**：页签选项卡、分类导航切换。
- **关键属性**：
  - `UITabGroup.mAutoCollectChildren`：自动收集子物体下的 `UITabItem`。
  - `UITabGroup.mAllowSwitchOff`：是否允许全部取消选中（开关模式允许，页签模式不允许）。
  - `UITabGroup.mContentPanels`：关联的内容节点列表，切换 Tab 时自动联动显隐。
  - `UITabItem.mVisualMode`：支持 `GameObjectToggle`（双节点显隐）、`ColorTint`（变色）、`SpriteSwap`（换图）及 `Custom`。
- **代码监听范例**：
  ```csharp
  [SerializeField] private UITabGroup mCategoryTabs;

  protected override void OnStart()
  {
      mCategoryTabs.TabChanged += (newIdx, oldIdx, isUser) =>
      {
          Debug.Log($"切换到分类: {mCategoryTabs.SelectedKey}");
          // Controller 驱动刷新对应分类数据
      };
      // 按键名选中指定页签
      mCategoryTabs.SelectByKey("Equip");
  }
  ```

#### 2. `UIButton`
- **定位**：统一按钮，增强 Unity 原生 `Button`。
- **特性**：
  - 内置按压缩放弹性过渡（`mEnableScaleEffect`）；
  - 防连点抖动（`mDebounceSeconds` 默认 0.15s）；
  - 异步网络 `Busy` 状态防重复点击拦截；
  - 技能/动作冷却计时（`BeginCooldown(seconds)`）与 `CooldownProgress` 进度回调。
- **代码范例**：
  ```csharp
  [SerializeField] private UIButton mConfirmBtn;

  private void OnEnable()
  {
      mConfirmBtn.Clicked += OnConfirmClicked;
  }

  private async void OnConfirmClicked()
  {
      if (!mConfirmBtn.TryBeginBusy()) return; // 锁定按钮，防连击

      try
      {
          await SendPurchaseRequestAsync();
      }
      finally
      {
          mConfirmBtn.EndBusy(); // 无论成功失败必须解锁
      }
  }
  ```

#### 3. `UIBusyState` 与 `UILoadingIndicator`
- **定位**：页面异步等待遮罩与加载转圈。
- **特性**：
  - `UIBusyState` 拦截射线（`blocksRaycasts = true`）；
  - 支持多并发请求嵌套计数（多次 `BeginBusy()` 对应多次 `EndBusy()`）；
  - 超时自动强制解除安全保护（默认 15s），防止网络未响应导致 UI 永久锁死。
- **标准用法（网游/单机通用铁律）**：
  - 每个需要异步拉数据的页面 Prefab 内嵌一个，遮罩**只盖内容区、不盖关闭按钮**（出口永远畅通）；
  - 加载中点关闭 = `CancellationTokenSource.Cancel()` 取消请求 + `ForceEndBusy()` 强制解除 + 关闭页面；
  - 异步返回处必须三重防护：已取消 / 页面已销毁 / 页面已隐藏 → 丢弃数据；
  - 全局资源加载（远程 AB）阶段的提示属于框架层全局 Loading，与本组件无关；
  - 只有不可逆的付费/提交操作才短暂锁死整个界面，且那属于按钮级 `UIButton.TryBeginBusy` 的职责。
- **代码范例**：
  ```csharp
  [SerializeField] private UIBusyState mBusyMask;

  public void OnRequestStart()
  {
      mBusyMask.BeginBusy("正在连接服务器...", timeoutSeconds: 10f);
  }

  public void OnRequestDone()
  {
      mBusyMask.EndBusy();
  }
  ```

#### 4. `UIPopupPresenter` 与 `UIMessagePresenter`
- **定位**：模态通用弹窗（适配 Concave `Popup Base`）与悬浮提示条（适配 Concave `Notification`）。
- **代码范例**：
  ```csharp
  [SerializeField] private UIPopupPresenter mPopup;
  [SerializeField] private UIMessagePresenter mToast;

  // 弹出确认框
  mPopup.Show(UIPopupOptions.CreateConfirm(
      message: "确认出售该装备？",
      title: "提示",
      onConfirm: () => {
          mToast.Show("出售成功", UIMessageType.Success);
      }
  ));
  ```

---

### 【P1 列表与 Item 核心展示】

#### 1. `UIListItem` 与 `UISimpleList`
- **定位**：轻量级列表展示（装备槽位、分类筛选、任务清单）。
- **特性**：
  - 内置对象池，增减数据时复用已实例化的 Item；
  - 支持单选互斥控制及反选。
- **代码范例**：
  ```csharp
  [SerializeField] private UISimpleList mSimpleList;

  public void Refresh(List<string> items)
  {
      mSimpleList.SetData(items);
      mSimpleList.SelectByIndex(0);
  }
  ```

#### 2. `UIVirtualList`（核心）
- **定位**：海量数据虚拟化滚动列表（如 1000+ 个背包格子或道具）。
- **特性**：
  - 无论数据源有多少条，只在视口（Viewport）内实例化可见数量项（如 10~20 个）；
  - 支持垂直/水平单列排布以及网格（Grid）多列排布；
  - 滚动时自动计算索引并复用池中节点，无卡顿无 GC。
- **代码范例**：
  ```csharp
  [SerializeField] private UIVirtualList mInventoryList;

  public void InitList(List<ItemData> items)
  {
      mInventoryList.SetData(items);
  }
  ```

#### 3. `UIItemIcon`、`UIItemCount` 与 `UIBadge`
- **定位**：背包格子内部积木组件。
  - `UIItemIcon`：主图标、品质框（`SetQualityBorder`）、未解锁置灰（`SetLocked`）。
  - `UIItemCount`：数量展示，小于等于1自动隐藏，支持超出上限截断（如 `999+`）。
  - `UIBadge`：红点提示与数字气泡（支持 `99+` 截断）。

#### 4. `UIProgressBar` 与 `UICooldownView`
- **定位**：
  - `UIProgressBar`：经验条、血条，支持平滑数值缓动（`SetProgress`）与富文本格式化。
  - `UICooldownView`：技能/道具冷却遮罩，支持 Radial 360 扇形与滑动遮罩，秒数自动格式化。

---

### 【P2 拖拽与输入控制】

#### 1. `UIDragHandle` 与 `UIDropTarget`
- **定位**：背包道具拖拽穿戴、技能拖拽至快捷栏。
- **特性**：
  - `UIDragHandle` 挂载在可拖拽项，拖拽开始时将 CanvasGroup 置为穿透状态并显示替身（`DragAvatar`）；
  - `UIDropTarget` 挂载在目标槽位，移入高亮，松开后分发 `Dropped(target, handle, payload)` 事件。
- **代码范例**：
  ```csharp
  // 槽位接收到物品放入
  dropTarget.Dropped += (slot, handle, payload) =>
  {
      if (payload is ItemData item)
      {
          // Controller 发送装备穿戴命令
          this.SendCommand(new EquipItemCommand(slot.TargetTag, item.Id));
      }
  };
  ```

#### 2. `UIInputField` 与 `UISlider`
- **`UIInputField`**：内置防抖机制（`mDebounceSeconds` 默认 0.2s），适合搜索框实时查询；支持字符数统计（如 `15/30`）。
- **`UISlider`**：支持离散步长吸附（`mStepSize`，如每次按 10 增减）与动态格式化。

---

### 【P3 统一表现与视窗交互】

#### 1. `UISelectableState`
- **定位**：复合状态机，一键切换 `Normal`、`Selected`、`Highlighted`、`Disabled`、`Busy`、`Locked` 6 大状态外观。

#### 2. `UIWindowAnimation`
- **定位**：挂载于弹窗根节点，提供 `ScaleFade`（缩放弹出回弹）、`SlideBottom`（底部滑入）等标准动效。

#### 3. `UIFocusController`
- **定位**：焦点与键盘/手柄 Tab 循环导航。

#### 4. `UITooltipController`
- **定位**：悬浮提示气泡，内置**智能屏幕边缘防遮挡检测（Screen Boundary Clamp）**：贴近屏幕右边界或下边界时自动向左/向上翻转，内容永不被遮挡。
