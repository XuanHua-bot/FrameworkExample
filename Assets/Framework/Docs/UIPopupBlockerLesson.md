# Popup 共享输入阻挡教学文档（已废弃）

> 本文描述的是旧的 Root 级共享遮罩方案，已经不符合当前代码实现。
> 当前实现改为：遮挡配置保存在 `UIPage`，遮挡对象由页面 Prefab 自己携带，`UGUIService` 在 `Show/Hide` 时控制它。
> 请不要继续按照本文修改代码；新的教学文档会基于当前实现重新编写。

## 当前模块进度（1/1）

当前 UGUI 已经支持：

- 页面资源加载和释放；
- Single / Multiple；
- Scene / Persistent；
- UGUIRoot 层级；
- 页面生命周期；
- 页面参数；
- 返回栈。

本阶段新增：

- PopupLayer 共享输入阻挡；
- Popup 打开时阻挡底层 UGUI；
- Popup 全部关闭后恢复底层 UGUI 输入。

本阶段不修改：

- UINavigationService；
- UIOpenMode；
- UILifetime；
- 页面返回栈；
- UI Toolkit；
- 对象池。

## 一、为什么使用共享遮罩

UGUI 的点击由 EventSystem 和 GraphicRaycaster 根据 UI Graphic 的射线结果决定。

因此，一个全屏透明的 Image 只要满足：

- 覆盖整个 Canvas；
- Image 的 Raycast Target 开启；
- 位于底层页面上方；
- 位于 Popup 内容下方；

就可以拦截底层按钮的点击。

本阶段不把遮罩重复放入每个 Popup prefab，而是在 UGUIRoot 的 PopupLayer 下准备一份共享对象：

~~~text
UGUIRoot
└── PopupLayer
    ├── PopupBlocker
    ├── ExampleBagPage
    └── ExampleConfirmPage
~~~

PopupBlocker 的作用是：

~~~text
底层页面
    ↓
PopupBlocker 拦截底层输入
    ↓
Popup 页面自身的按钮正常接收输入
~~~

使用共享对象的原因：

- 所有 Popup 使用同一套遮罩规则；
- 不需要每个 Popup prefab 重复制作全屏 Panel；
- 多个 Popup 同时存在时不需要维护多份遮罩；
- 遮罩排序由 UGUIRoot 和 UGUIService 统一控制。

## 二、页面层级规则

本阶段约定：

~~~text
Popup：默认阻挡底层 UGUI 输入
System：默认阻挡底层 UGUI 输入
Normal：不阻挡底层 UGUI 输入
Background：不阻挡底层 UGUI 输入
Toast：不阻挡底层 UGUI 输入
~~~

这里的“阻挡输入”只针对 UGUI：

- Button；
- Toggle；
- Slider；
- Image；
- GraphicRaycaster。

它不会自动停止 Physics.Raycast、角色移动输入或自定义战斗输入。

## 三、修改 UGUIRoot.cs

### 文件路径

~~~text
E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\UGUI\UGUIRoot.cs
~~~

### 第 1 处：新增字段

找到当前字段：

~~~csharp
[SerializeField] private Transform mSystemLayer;
[SerializeField] private Transform mToastLayer;
~~~

在后面新增：

~~~csharp
[Header("Popup")]
[SerializeField] private GameObject mPopupBlocker;
~~~

这个字段保存 UGUIRoot prefab 中的 PopupBlocker 对象。

### 第 2 处：新增控制方法

找到当前属性：

~~~csharp
public Transform PopupLayer => mPopupLayer;
~~~

在这个属性后面新增：

~~~csharp
/// <summary>
/// 控制 PopupLayer 的共享输入阻挡对象。
/// PopupBlocker 必须位于 PopupLayer 下，并排在所有 Popup 页面之前。
/// </summary>
public void SetPopupBlocker(bool active)
{
    if (mPopupBlocker == null)
    {
        return;
    }

    if (active)
    {
        // 保证遮罩位于 Popup 页面下面，不盖住 Popup 自身按钮。
        mPopupBlocker.transform.SetAsFirstSibling();
    }

    mPopupBlocker.SetActive(active);
}
~~~

只新增字段和方法，不要替换整个 UGUIRoot.cs。

## 四、修改 UGUIRoot.prefab

### 文件路径

~~~text
E:\unityXiangmu\FrameworkExample\Assets\Framework\Prefabs\UI\UGUIRoot.prefab
~~~

在 PopupLayer 下创建：

~~~text
PopupBlocker
~~~

给 PopupBlocker 添加：

- RectTransform；
- Image。

RectTransform 设置：

~~~text
Anchor Min：0, 0
Anchor Max：1, 1
Left：0
Right：0
Top：0
Bottom：0
~~~

Image 设置：

~~~text
颜色：透明
Raycast Target：开启
~~~

PopupLayer 的层级顺序必须是：

~~~text
PopupLayer
├── PopupBlocker
└── 其他 Popup 页面
~~~

将 PopupBlocker 拖到 UGUIRoot 组件的：

~~~text
Popup Blocker
~~~

字段中，并把 PopupBlocker 默认设置为 inactive。

## 五、修改 UGUIService.cs

### 文件路径

~~~text
E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\UGUI\UGUIService.cs
~~~

本阶段修改：

- Show(UIPage page)；
- Hide(UIPage page)；
- Close(UIPage page)；
- 新增 RefreshPopupBlocker()。

### 第 1 处：修改 Show(UIPage page)

找到：

~~~csharp
page.transform.SetAsLastSibling();
page.OnShow();
~~~

在后面新增：

~~~csharp
RefreshPopupBlocker();
~~~

修改后末尾为：

~~~csharp
page.transform.SetAsLastSibling();
page.OnShow();
RefreshPopupBlocker();
~~~

### 第 2 处：修改 Hide(UIPage page)

找到：

~~~csharp
page.OnHide();
page.gameObject.SetActive(false);
~~~

在后面新增：

~~~csharp
RefreshPopupBlocker();
~~~

修改后为：

~~~csharp
page.OnHide();
page.gameObject.SetActive(false);
RefreshPopupBlocker();
~~~

### 第 3 处：修改 Close(UIPage page)

找到：

~~~csharp
Hide(page);
mOpenedPages.Remove(page);
~~~

在 mOpenedPages.Remove(page); 后面新增：

~~~csharp
RefreshPopupBlocker();
~~~

修改后为：

~~~csharp
Hide(page);
mOpenedPages.Remove(page);
RefreshPopupBlocker();
~~~

原有的 Handle、Single 字典、OnClose、pageClosed、Destroy 和 Release 逻辑全部保留。

### 第 4 处：新增 RefreshPopupBlocker()

在 Hide(UIPage page) 方法后面，新增：

~~~csharp
private void RefreshPopupBlocker()
{
    if (mRoot == null)
    {
        return;
    }

    var shouldBlockInput = false;

    foreach (var pair in mOpenedPages)
    {
        var page = pair.Key;

        // 被关闭或销毁的 Unity 对象可能表现为 null。
        if (page == null || !page.gameObject.activeSelf)
        {
            continue;
        }

        var attribute = GetPageAttribute(page.GetType());
        if (attribute == null)
        {
            continue;
        }

        if (attribute.Layer == UILayer.Popup
            || attribute.Layer == UILayer.System)
        {
            shouldBlockInput = true;
            break;
        }
    }

    mRoot.SetPopupBlocker(shouldBlockInput);
}
~~~

判断流程：

~~~text
打开 Popup 或 System 页面
    ↓
启用 PopupBlocker

没有可见 Popup 或 System 页面
    ↓
关闭 PopupBlocker
~~~

## 六、为什么 Show、Hide、Close 都要刷新

打开 Popup A：

~~~text
Popup A 显示
    ↓
PopupBlocker 开启
~~~

再打开 Popup B：

~~~text
Popup B 显示
    ↓
PopupBlocker 继续开启
~~~

关闭 Popup B：

~~~text
Popup A 仍然可见
    ↓
PopupBlocker 继续开启
~~~

关闭 Popup A：

~~~text
没有可见 Popup
    ↓
PopupBlocker 关闭
~~~

Show、Hide、Close 都可能改变“当前可见的阻挡页面”，所以都需要刷新。

## 七、页面调用方式不变

页面自己的关闭按钮仍然由页面脚本处理：

~~~csharp
private void CloseSelf()
{
    this.GetUtility<IUGUIService>().Close(this);
}
~~~

导航页面返回仍然使用：

~~~csharp
this.GetUtility<IUINavigationService>().Back();
~~~

打开页面时，调用方仍然不判断 Single 或 Multiple：

~~~csharp
this.GetUtility<IUGUIService>().OpenAsync<ExampleScenePage>(
    page => { },
    reason => Debug.LogError(reason));
~~~

页面的层级由特性声明：

~~~csharp
[UIPage(
    "UI/ExampleScenePage",
    UIOpenMode.Single,
    UILayer.Popup,
    UILifetime.Scene)]
~~~

因为它使用 Popup 层，所以显示后会启用共享 PopupBlocker。

## 八、本阶段不做的功能

本阶段不加入：

- 点击遮罩自动关闭；
- 遮罩动画；
- 自动修改 Time.timeScale；
- 自动暂停战斗逻辑；
- Physics.Raycast 输入锁定；
- UI Toolkit 输入阻挡；
- 每个 Popup 自己维护一份遮罩；
- 对象池。

点击遮罩关闭属于具体页面行为。框架当前只负责拦截底层 UI 输入。

## 九、手动验证

### 1. Popup 页面

打开 ExampleScenePage：

~~~text
ExampleScenePage 位于 PopupLayer
PopupBlocker 被启用
控制面板上的底层按钮不响应
ExampleScenePage 自己的按钮仍然响应
~~~

### 2. Multiple 页面

打开 ExampleMultiplePage：

~~~text
ExampleMultiplePage 位于 ToastLayer
PopupBlocker 不会因为它打开而启用
控制面板仍然可以点击
每次打开仍然创建新的 Multiple 实例
~~~

### 3. 多个 Popup

~~~text
打开 Popup A：遮罩开启
打开 Popup B：遮罩保持开启
关闭 Popup B：遮罩仍开启
关闭 Popup A：遮罩关闭
~~~

### 4. 场景切换

打开一个 UILifetime.Scene 的 Popup，然后切换场景：

~~~text
Scene Popup 被 UGUIService.CloseScenePages 关闭
PopupBlocker 关闭
新场景底层 UI 可以接收输入
~~~

## 十、总结

本阶段新增的是一份由 UGUIRoot 持有、由 UGUIService 控制的共享输入阻挡对象。

职责关系：

~~~text
UGUIRoot
    保存 PopupBlocker 引用

UGUIService
    根据当前可见页面刷新 PopupBlocker

UIPageAttribute
    通过 UILayer 表示页面属于 Popup、System 或其他层

具体页面
    只负责自身按钮和关闭逻辑
~~~

这样非全屏 Popup 也能占据整个输入范围，同时保持页面视觉尺寸不变。

本阶段没有修改导航栈，也没有把遮罩逻辑塞进页面脚本。
