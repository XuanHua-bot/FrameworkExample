# UI 框架第 1 阶段：UI 回归测试手抄教程

## 一、先修复这次 InputBlocker 失效

你遇到的问题有两个具体原因。当前磁盘上的 `ExamplePersistentPage.prefab` 根物体同时挂了 `UIPage` 和 `ExamplePersistentPage` 两个组件。你打开的是单独 `UIPage` 组件的 `M Blocks Underlying Input`；真正被 `UGUIService` 取得并显示的是 `ExamplePersistentPage` 组件，其值仍为 false。其次，两个组件的 `M Input Blocker` 都是 None。只打勾而没有遮挡对象，原来的 `SetInputBlocker()` 会直接返回。

在 Unity 的 Prefab Mode 中打开：

    Assets/Example/Resources/UI/ExamplePersistentPage.prefab

选根物体 `ExamplePersistentPage`，执行：

    1. 删除多余的 UIPage 组件，保留 ExamplePersistentPage 组件。
    2. 在 ExamplePersistentPage 组件上勾选 M Blocks Underlying Input。
    3. 保存 Prefab。

`ExampleScenePage` 等其他 Prefab 也存在“基类 UIPage + 具体页面脚本”同时挂载的情况。回归测试前同样检查：只保留具体页面脚本，在那个组件上设置遮挡属性。不要手工编辑 Prefab 的 YAML。

## 二、框架代码：没有手动拖遮挡对象时自动创建

修改文件：

    Assets/Framework/Runtime/UI/UGUI/UIPage.cs

文件顶部加入：

    using UnityEngine.UI;

在 `Awake()` 取得 `CanvasGroup` 后调用：

    EnsureInputBlocker();

在 `SetInputBlocker(bool active)` 开头调用：

    EnsureInputBlocker();

再在 `UIPage` 类中加入以下方法。自动创建的遮挡物放在页面所在 Layer 中，紧贴页面下面。原因是弹窗根节点可能只有 720 宽，直接把全屏遮挡物放到根节点下仍然盖不住整个 Canvas。

    private void EnsureInputBlocker()
    {
        if (!mBlocksUnderlyingInput || mInputBlocker != null)
        {
            return;
        }

        var blocker = new GameObject("InputBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var blockerParent = transform.parent != null ? transform.parent : transform;
        blocker.transform.SetParent(blockerParent, false);
        if (blockerParent == transform)
        {
            blocker.transform.SetAsFirstSibling();
        }
        else
        {
            blocker.transform.SetSiblingIndex(transform.GetSiblingIndex());
        }

        var rect = blocker.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = blocker.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        mInputBlocker = blocker;
        blocker.SetActive(false);
    }

`UIPage` 销毁时，自动创建的对象是兄弟节点，不会跟着页面自动销毁。加入：

    protected virtual void OnDestroy()
    {
        if (mInputBlocker != null && mInputBlocker.transform.parent != transform)
        {
            Destroy(mInputBlocker);
        }
    }

原有 `UGUIService.Show()` 和 `Hide()` 已分别调用：

    page.SetInputBlocker(true);
    page.SetInputBlocker(false);

上面代码目前只写在教学文档中，项目源码已经恢复为修改前版本。请你手抄确认后，再由你决定是否加入项目。Prefab 已手工配置遮挡对象时，`EnsureInputBlocker()` 不会额外创建对象。

## 三、写一个最小回归测试入口

新建文件：

    Assets/Example/Scripts/UIRegressionTestController.cs

手抄完整代码：

    using GameFramework;
    using UnityEngine;
    using UnityEngine.UI;

    namespace GameFramework.Examples
    {
        public sealed class UIRegressionTestController : MonoBehaviour, IController
        {
            [ContextMenu("Open Persistent and Check Blocker")]
            private void OpenPersistentAndCheckBlocker()
            {
                this.GetUtility<IUGUIService>().OpenAsync<ExamplePersistentPage>(page =>
                {
                    Debug.Assert(page.BlocksUnderlyingInput,
                        "[UI回归] 请在 ExamplePersistentPage 组件上勾选 M Blocks Underlying Input");

                    var parent = page.transform.parent;
                    var index = page.transform.GetSiblingIndex();
                    var blocker = index > 0 ? parent.GetChild(index - 1) : null;
                    var image = blocker != null ? blocker.GetComponent<Image>() : null;
                    Debug.Assert(image != null && image.raycastTarget && blocker.gameObject.activeInHierarchy,
                        "[UI回归] 页面下方没有生效的 InputBlocker");
                }, reason => Debug.LogError("[UI回归] 打开失败: " + reason));
            }

            IArchitecture IBelongArchitecture.GetArchitecture()
            {
                return ExampleArchitecture.Interface;
            }
        }
    }

把脚本挂在场景空物体上。进入 Play Mode 后，从组件右上角菜单执行 `Open Persistent and Check Blocker`。Console 无断言错误后，再手动点击弹窗外的底层按钮；这些按钮应不再触发。测试脚本只做状态断言，实际点击仍需手动验证。

## 四、检查 Popup Prefab 配置

先打开以下 Prefab：

    Assets/Example/Resources/UI/ExampleScenePage.prefab
    Assets/Example/Resources/UI/ExampleDetailPage.prefab

进入 Prefab Mode，选择根节点上的具体页面脚本，确认没有额外挂一个 `UIPage` 组件，并检查：

    有 CanvasGroup
    具体页面组件的 Blocks Underlying Input = true
    Input Blocker 可以留空，由 UIPage 自动创建

如果你选择手工配置 InputBlocker，则确认它的子物体：

    有 RectTransform
    Anchor Min = (0, 0)
    Anchor Max = (1, 1)
    Left / Right / Top / Bottom = 0
    有 Image
    Image.Raycast Target = true
    默认 Active = false

手工配置时，必须把对象拖入具体页面组件的 Input Blocker 字段。

如果手动配置 InputBlocker，建议它位于页面内容下面：

    ExampleScenePage
    ├── InputBlocker
    └── Popup - Size Fitter
        ├── Content
        └── Buttons

当前 UIPage 的自动创建版本会把 InputBlocker 创建到页面所在的 Layer，并放在页面前面。这样即使页面根节点只是 720 宽，遮挡也能覆盖整个 Layer。

## 五、运行前检查场景

打开场景：

    Assets/Example/Scenes/FrameworkExample.unity

确认 Hierarchy 中存在：

    UGUIRoot
    ├── BackgroundLayer
    ├── NormalLayer
    ├── PopupLayer
    ├── SystemLayer
    └── ToastLayer

还要确认：

    ExampleControlPanel 在场景中
    ExampleUGUIRootBootstrap 在 UGUIRoot 上或其同一对象上
    EventSystem 在场景中

ExampleSceneEntry 当前不自动打开页面，启动后应先看到控制面板，页面由控制面板按钮打开。

## 六、测试 1：打开 Popup 页面

运行场景，点击控制面板的“打开导航页面 A”。

预期：

    ExampleScenePage 出现在 PopupLayer
    页面显示 Popup - Size Fitter 视觉结构
    页面文本显示“导航页面 A”
    InputBlocker 从 inactive 变为 active

点击弹窗自己的“进入页面 B”和“关闭”按钮，两个按钮都应响应。

## 七、测试 2：验证底层输入是否被挡住

保持 ExampleScenePage 打开，点击弹窗外的控制面板区域。

预期：

    控制面板按钮不响应
    ExampleScenePage 自己的按钮仍然响应

如果控制面板仍然响应，按顺序检查：

    1. 根节点 Blocks Underlying Input 是否为 true
    2. Input Blocker 是否拖入 UIPage 的 Input Blocker 字段
    3. InputBlocker 是否在 Popup 页面根节点下
    4. InputBlocker 的 Image.Raycast Target 是否开启
    5. InputBlocker 是否排在 Content 前面
    6. Popup 页面是否真的位于 PopupLayer

如果弹窗自己的按钮也不响应，优先检查层级顺序：InputBlocker 必须在内容下面。

如果底层按钮仍然能点击，再检查底层按钮是否属于另一个 Canvas。另一个 Canvas 的 sortingOrder 高于当前页面时，页面内的 InputBlocker 无法覆盖它，需要统一到同一个 UGUI Canvas，或调整 Canvas 的 sortingOrder。

## 八、测试 3：导航 A → B → 返回

重新运行场景，点击：

    打开导航页面 A
    进入页面 B

当前 UINavigationService 的设计是：进入 B 时隐藏 A，而不是让 A、B 同时可见。

预期：

    A 页面隐藏
    B 页面显示
    B 页面按钮可以点击
    A 页面按钮暂时不可点击

点击 B 页面的返回按钮。

预期：

    B 页面关闭并销毁
    B 的资源 Handle 释放
    A 页面重新显示
    A 页面按钮恢复响应

这里测试的是：

    IUINavigationService.Back()

不要用 IUGUIService.Close<ExampleDetailPage>() 替代返回测试。直接 Close 只测试页面关闭，不测试导航服务恢复上一页的逻辑。

## 九、测试 4：关闭导航根页

打开 A 页面，点击它自己的“关闭”按钮。

预期：

    A 页面执行 OnClose
    A 页面从 UGUIService 登记表移除
    A 页面销毁
    A 页面资源 Handle 释放
    InputBlocker 关闭
    控制面板恢复点击

如果页面消失后控制面板仍不能点击，检查 Hide() 是否执行：

    page.SetInputBlocker(false)

## 十、测试 5：Multiple 页面

点击控制面板的“打开 Multiple 页面”，连续点击两次或三次。

预期：

    每次点击都创建新的 ExampleMultiplePage 实例
    每个实例显示不同编号
    关闭其中一个实例不会关闭其他实例

Multiple 页面当前位于 ToastLayer，所以它不应被当成 Popup 模态页面。除非在它自己的 Prefab 上主动配置 Blocks Underlying Input，否则底层控制面板仍可点击。

## 十一、测试 6：Persistent 页面

点击“打开 Persistent 页面”，然后点击“切换场景”。

预期：

    Persistent 页面仍然存在
    Scene 页面被 UGUIService 关闭
    新场景的 UGUIRoot 重新绑定
    控制面板仍然可以使用

再点击“关闭 Persistent 页面”。预期页面被销毁，之后再次打开时得到新的页面实例。

## 十二、测试 7：资源加载失败路径

这一步不修改正式页面。临时复制一个页面脚本，把 UIPageAttribute 的资源路径改成：

    UI/DoesNotExist

然后点击打开它。

预期：

    打开失败回调收到错误信息
    场景不会产生空页面
    后续再次点击其他按钮仍然可用

测试完成后恢复错误路径，不要把临时代码留在正式示例中。

## 十三、测试 8：场景切换期间的页面清理

执行：

    打开 ExampleScenePage
    立即点击切换场景
    等待新场景加载完成

预期：

    Scene 页面不会残留在新场景
    Persistent 页面如果存在则继续保留
    旧场景的异步 UI 加载回调不会在新场景错误创建页面

这一项验证 UGUIService 的 mSceneVersion 和 UILifetime.Scene 配合。

## 十四、记录测试结果

每次修改 UI 后，复制并填写下面表格：

| 测试项 | 结果 | 备注 |
| --- | --- | --- |
| 打开 ExampleScenePage | □通过 □失败 | |
| InputBlocker 拦截底层按钮 | □通过 □失败 | |
| A → B | □通过 □失败 | |
| B → Back → A | □通过 □失败 | |
| 关闭 A | □通过 □失败 | |
| Multiple 多实例 | □通过 □失败 | |
| Persistent 跨场景 | □通过 □失败 | |
| 资源加载失败回调 | □通过 □失败 | |
| 场景切换期间清理 | □通过 □失败 | |

## 十五、当前测试边界

本阶段测的是现有代码已经声明的行为：

    UGUIService：加载、创建、显示、隐藏、关闭、释放
    UINavigationService：Push、Back、BackTo
    UIPage：生命周期、CanvasGroup、页面级 InputBlocker
    ResourcesAssetLoader：缓存、异步加载、Handle 引用计数

以下行为当前还没有实现，不要写成“测试通过”：

    多个同时可见 Popup 的全局栈顶交互
    统一的 Popup 栈
    点击遮罩自动关闭
    键盘或手柄焦点恢复
    Physics 输入暂停
    场景加载进度 UI

等本阶段回归测试全部通过后，再单独设计 SceneService，把场景加载、UI 遮挡和导航栈分开推进。

