# UI 资源包接入教学

> 当前示例修正：直接把 `Dark - Info.prefab` 的视觉内容接入现有 `ExampleMultiplePage.prefab`，因为示例场景主要演示 Popup 和 Button。不会再通过 Controller 运行时实例化 `DarkInfo(Clone)`。旧版 `Dark - Equipment.prefab` 属于资源包 Popup 演示页，直接由框架实例化后点击关闭会触发其未初始化的 `m_background`，导致 `Popup.RemoveBackground()` 空引用。

## 一、先说结论

`Ultimate Clean GUI Pack` 不应该直接放进：

```text
Assets/Framework/Runtime
Assets/Framework/Prefabs
```

它属于视觉资源和 UI 组件资源，不属于通用框架核心。

推荐的目录关系是：

```text
Assets
├── UltimateCleanGUIPack
│   ├── Common
│   └── Themes
├── Example
│   └── UI
│       ├── Controllers
│       └── Prefabs
└── Framework
    ├── Runtime
    └── Prefabs
```

三者职责：

```text
ThirdParty
    保存购买资源包原始内容，不修改供应商 Prefab

Example/UI
    用资源包组件组成当前项目自己的页面

Framework
    管理页面、资源、层级、生命周期和导航，不依赖某个主题
```

## 二、什么叫“接入”

接入不是把所有 Prefab 注册到框架里。

接入是：

```text
资源包视觉 Prefab
    ↓ 作为子物体放入
项目页面 Prefab
    ↓ 页面根物体挂
项目自己的 UIPage Controller
    ↓ 由
UGUIService 加载和管理
```

本资源包没有字面名称为 `Black` 的目录；这里把你说的 Black Style 对应为资源包中的 `Dark` 主题。

例如资源包中的：

```text
Themes/Dark/Prefabs/Popup Demos/Universal/Dark - Equipment.prefab
```

不直接让业务代码打开这个资源，而是制作：

```text
Assets/Example/Resources/UI/EquipmentPopup.prefab
```

它的层级类似：

```text
EquipmentPopup                 项目自己的根物体
├── EquipmentPopupController   继承 UIPage
├── CanvasGroup                UIPage 自动准备
├── InputBlocker               项目自己的输入遮挡
└── VendorEquipmentView        资源包的 Equipment 视觉 Prefab
```

这样以后替换主题时，只替换 `VendorEquipmentView`，页面 Controller、导航代码和资源生命周期不用重写。

## 三、为什么不直接修改资源包 Prefab

直接修改供应商 Prefab 会产生三个问题：

```text
1. 资源包更新时，自己的修改可能被覆盖。
2. 框架核心会开始依赖某个具体主题的层级和脚本。
3. 以后换 Dark、Modern Light 或 Fantasy 时，业务代码会被绑定。
```

所以采用“组合而不是修改”：

```text
供应商 Prefab = 视觉组件
项目 Wrapper Prefab = 页面边界
UIPage Controller = 页面业务和生命周期
UGUIService = 实例和资源管理
```

## 四、资源包导入位置

原始资源包保留在项目资源区，框架只携带从 Concave 场景依赖链提取出的独立副本：

```text
Assets/UltimateCleanGUIPack
Assets/Framework/UI/ComponentLibrary/Concave
```

Concave 副本的入口清单是：

```text
Assets/Framework/UI/ComponentLibrary/Concave/ComponentLibraryManifest.json
```

Concave 副本使用新 GUID，Prefab 的资源引用已改写到副本内部；原始资源包继续供旧示例场景使用。副本中的表现脚本已经改用 `GameFramework.Concave` 命名空间，框架行为仍需逐项适配，不能将原包脚本自动视为本框架实现。

不要移动到：

```text
Assets/Framework/Runtime
```

因为这里存放框架的 C# 运行时代码，不适合混入整套第三方主题、演示场景和源文件。

## 五、第一条接入样例：Equipment Popup

### 5.1 创建项目 Controller

文件：

```text
Assets/Example/Scripts/EquipmentPopupController.cs
```

代码：

```csharp
using GameFramework;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Examples
{
    [UIPage(
        "UI/EquipmentPopup",
        UIOpenMode.Single,
        UILayer.Popup,
        UILifetime.Scene)]
    public sealed class EquipmentPopupController : UIPage, IController
    {
        [SerializeField]
        private Button mCloseButton;

        public override void OnOpen()
        {
            if (mCloseButton != null)
            {
                mCloseButton.onClick.AddListener(CloseSelf);
            }
        }

        public override void OnClose()
        {
            if (mCloseButton != null)
            {
                mCloseButton.onClick.RemoveListener(CloseSelf);
            }
        }

        private void CloseSelf()
        {
            this.GetUtility<IUGUIService>().Close(this);
        }

        IArchitecture IBelongArchitecture.GetArchitecture()
        {
            return ExampleArchitecture.Interface;
        }
    }
}
```

这里的资源 Key 指向项目 Wrapper Prefab：

```text
Resources/UI/EquipmentPopup.prefab
```

它不指向第三方的 `Dark - Equipment.prefab`。

### 5.2 创建 Wrapper Prefab

在 Unity 中：

```text
1. 创建一个空的 UI GameObject，命名为 EquipmentPopup。
2. 添加 EquipmentPopupController。
3. 按 UIPage 的 RequireComponent 自动添加 CanvasGroup。
4. 添加项目自己的全屏 InputBlocker。
5. 将资源包的 Dark - Equipment.prefab 复制为项目内容资源：
   `Assets/Example/Resources/UI/Vendor/DarkEquipment.prefab`。
6. 保存项目 Wrapper 为 `Assets/Example/Resources/UI/EquipmentPopup.prefab`。
7. `EquipmentPopupController.OnOpen()` 会加载并实例化 Dark 视觉 Prefab。
```

最终层级示意：

```text
EquipmentPopup
├── EquipmentPopupController
└── CanvasGroup                 页面根物体上的组件

运行时实例化后：

```text
EquipmentPopup
└── Dark - Equipment           Resources/UI/Vendor/DarkEquipment.prefab
```
```

如果资源包 Prefab 自己已经带了 `Canvas`，先保留它，不要为了接入强行把所有 Canvas 删除。
是否需要独立 Canvas，应通过实际绘制顺序和批处理结果验证。

### 5.3 业务代码打开页面

业务代码只依赖项目页面类型：

```csharp
private void OpenEquipmentPopup()
{
    this.GetUtility<IUGUIService>().OpenAsync<EquipmentPopupController>(
        page => Debug.Log("Equipment Popup opened: " + page.name),
        reason => Debug.LogError("Equipment Popup failed: " + reason));
}
```

业务代码不出现：

```csharp
Instantiate(...)
Resources.Load(...)
"Dark - Equipment.prefab"
```

## 六、资源包脚本怎样处理

资源包中可能包含：

```text
Popup.cs
PopupOpener.cs
InfoPopup.cs
Notification.cs
Transition.cs
CleanButton.cs
```

处理原则：

```text
纯视觉和交互组件：可以直接保留
依赖资源包全局管理器的脚本：先不要接入核心流程
与我们 UIPage 生命周期重复的脚本：由 Wrapper Controller 统一协调
```

例如资源包自己的 `PopupOpener` 可以继续负责按钮动画，但页面打开和关闭仍由：

```text
UGUIService
EquipmentPopupController
```

负责。

不要让资源包脚本反过来创建新的全局 UI 管理器。

## 七、主题替换方式

项目 Controller 不变：

```csharp
EquipmentPopupController
```

只替换 Wrapper Prefab 的子物体：

```text
        Dark - Equipment.prefab
        ↓ 替换为
其他主题的 Equipment.prefab
```

页面资源 Key 仍然保持：

```text
UI/EquipmentPopup
```

这就是资源和框架解耦：

```text
页面类型不变
导航调用不变
资源 Key 不变
只替换视觉子 Prefab
```

## 八、第一阶段不做的事情

当前不做：

```text
1. 不把 4748 个资源包 Prefab 全部注册进 UGUIService。
2. 不把资源包的 Popup.cs 改成我们的 UIPage 基类。
3. 不把资源包的主题枚举加入 UILayer。
4. 不让 Framework Runtime 引用 UltimateCleanGUIPack 命名空间。
5. 不为了显示效果把每个资源包 Prefab 都单独做成页面。
```

只为真实页面创建 Wrapper：

```text
EquipmentPopup
SettingsPage
ConfirmPopup
LoadingView
ToastView
```

## 九、接入验收

第一条样例完成后验证：

```text
1. 业务代码只能打开 EquipmentPopupController。
2. UGUIService 能按 UI/EquipmentPopup 加载 Wrapper Prefab。
3. Wrapper 内部显示资源包的 Equipment 视觉内容。
4. Popup 可以阻挡底层页面输入。
5. 关闭 Popup 后实例销毁并释放 Handle。
6. 替换主题子 Prefab 后，Controller 和业务调用不变。
7. Framework Runtime 不出现 UltimateCleanGUIPack 的具体类型引用。

当前示例场景的第一屏流程是：

```text
ExampleSceneEntry.Start
    -> 隐藏旧的 ExampleControlPanel 测试面板
    -> 通过 IUGUIService 打开 EquipmentPopupController
    -> 第一屏直接显示 Dark - Equipment 视觉 Prefab
```

旧 `ExampleControlPanel` 只保留给后续调试场景使用，不再作为资源包接入场景的主视觉。
```

## 总结

本项目接入 UI 资源包的核心方式是：

```text
第三方资源包
    作为视觉内容

项目 Wrapper Prefab
    作为资源和页面之间的适配边界

UIPage Controller
    负责业务事件和页面生命周期

UGUIService
    负责加载、显示、关闭和释放
```

这样资源包可以换，框架不用换；框架可以升级，页面视觉也不用全部重做。
