# UI 组件库实作：把 Concave 基础按钮接入框架

> 当前批次已直接修改组件库 Prefab 和脚本。验收、运行测试和示例场景接入暂缓，先继续完成组件迁移。

## 这篇文档要完成什么

这是一篇可以照着操作的实作文档。

本次只完成一个垂直功能：

```text
Concave 的基础按钮外观
    + Unity Button 的视觉过渡
    + GameFramework.UIButton 的状态和事件
    + 页面 Controller 的业务点击
    + IAudioService 的统一音效请求
```

本次已经直接修改项目中的组件资源。文档保留代码职责和迁移依据，方便后续继续手抄和复查。

## 一、先确认当前代码的职责

### 1. Concave 原按钮里有什么

当前基础 Prefab：

```text
Assets/Framework/UI/ComponentLibrary/Concave/Controls/Buttons/Button - Filled With Border.prefab
```

它已经包含：

| 内容 | 保留策略 | 原因 |
| --- | --- | --- |
| Image、Outline、Text/TMP、美术 Sprite | 保留 | 这是 Concave 的视觉资源 |
| UnityEngine.UI.Button | 保留 | Animator 过渡和目标 Graphic 依赖它 |
| Animator | 保留 | 这个基础按钮已经有 Normal、Highlighted、Pressed 等状态过渡 |
| CanvasGroup | 保留 | 可以参与页面显示和交互控制 |
| CleanButton | 移除 | 它只是资源包自己的 Button 子类，会重复处理点击声音 |
| ButtonSounds | 移除 | 它直接操作 AudioSource，绕过框架音频服务 |
| AudioSource | 移除 | 点击声音由 IAudioService 统一播放和管理 |

### 2. `UIButton` 已经负责什么

当前文件：

```text
Assets/Framework/Runtime/UI/Components/UIButton.cs
```

它已经提供：

- `Clicked`：一次有效点击完成后通知页面 Controller。
- `SoundRequested`：按钮配置了 `AudioCueKey` 后，请求外部音频服务播放。
- `TryBeginBusy()`：开始网络请求时锁定按钮，避免重复提交。
- `EndBusy()`：成功、失败、超时、取消时解除锁定。
- `SetInteractable(bool)`：由页面或系统统一设置可用状态。

这里有一个重要边界：`UIButton` 不直接依赖 `AudioService`。按钮只发出请求，页面 Controller 或专门的 UI 音频绑定器负责调用 `IAudioService`。

## 二、组件库验收（暂缓执行）

在把任何按钮接入页面之前，先确认组件库本身没有坏引用。

### 1. 在 Unity 中检查编译

打开 Unity 后先看 Console：

```text
没有红色编译错误
没有 Missing Script
```

黄色警告先记录，不要把黄色警告当成编译失败。

### 2. 手动检查五个代表性 Prefab

依次打开下面的 Prefab：

```text
Assets/Framework/UI/ComponentLibrary/Concave/Controls/Buttons/Button - Filled With Border.prefab
Assets/Framework/UI/ComponentLibrary/Concave/Controls/Buttons/Button - Filled.prefab
Assets/Framework/UI/ComponentLibrary/Concave/Controls/Buttons/Button - Icon.prefab
Assets/Framework/UI/ComponentLibrary/Concave/Controls/Inputs/Input Field.prefab
Assets/Framework/UI/ComponentLibrary/Concave/Feedback/Tooltips/Tooltip.prefab
```

每个 Prefab 都检查：

1. Inspector 顶部没有 `Missing (Mono Script)`。
2. Scene 视图能看到完整的 Sprite、文字和边框。
3. 运行时按下按钮，颜色或 Animator 状态能够变化。
4. Tooltip 的文字字段不是空引用。

### 3. 可选：加入编辑器批量检查菜单

如果不想一个个打开 Prefab，可以新建文件：

```text
Assets/Framework/Editor/ConcavePrefabAudit.cs
```

完整代码如下：

```csharp
using UnityEditor;
using UnityEngine;

namespace GameFramework.EditorTools
{
    /// <summary>
    /// 批量检查组件库 Prefab 是否包含丢失的 MonoBehaviour。
    /// 这个工具只在 Unity 编辑器中运行，不会进入运行时程序集。
    /// </summary>
    public static class ConcavePrefabAudit
    {
        private const string Root = "Assets/Framework/UI/ComponentLibrary/Concave";

        [MenuItem("Tools/Framework/Validate Concave Prefabs")]
        private static void Validate()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { Root });
            int missingCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = null;

                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);

                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        int count = GameObjectUtility
                            .GetMonoBehavioursWithMissingScriptCount(child.gameObject);

                        missingCount += count;

                        if (count > 0)
                        {
                            Debug.LogError(
                                $"Prefab 存在丢失脚本：{path}，对象：{child.name}，数量：{count}");
                        }
                    }
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            if (missingCount == 0)
            {
                Debug.Log($"Concave Prefab 检查通过，共检查 {guids.Length} 个 Prefab。");
            }
            else
            {
                Debug.LogError($"Concave Prefab 检查失败，丢失脚本总数：{missingCount}。");
            }
        }
    }
}
```

回到 Unity 后执行：

```text
Tools > Framework > Validate Concave Prefabs
```

只有 Console 输出“检查通过”时，才继续下面的 Prefab 改造。

## 三、当前已完成的按钮迁移

当前直接改造的 Prefab 是：

```text
Assets/Framework/UI/ComponentLibrary/Concave/Controls/Buttons/Button - Filled With Border.prefab
Assets/Framework/UI/ComponentLibrary/Concave/Containers/Popups/Popup Base.prefab
```

这两个 Prefab 的视觉资源和 Animator 保留，交互入口已经加入 `UIButton`。

## 四、把资源包按钮改成框架按钮

### 1. 保留 Unity Button 和 Animator

在 Prefab Mode 中选中根对象。确认根对象仍然有：

```text
Image
Button
Animator
CanvasGroup
```

`Button` 的 `Transition` 当前是 `Animation`，`Animator Controller` 仍然引用 Concave 的按钮控制器。不要因为换成 `UIButton` 就删除 Unity `Button`；`UIButton` 是协作组件，不是 Unity Button 的替代品。

### 2. 已移除资源包自己的声音链路

已从按钮相关对象移除或替换下面的资源包逻辑：

```text
CleanButton
ButtonSounds
AudioSource
```

迁移后的保留内容：

- `Button` 的 `Target Graphic` 仍然指向根对象的 Image。
- `Animator` 仍然存在。
- `Animator Controller` 没有变成空引用。
- Prefab 中没有其他对象依赖被删掉的 AudioSource。

### 3. 已添加 `UIButton`

两个目标 Prefab 的按钮对象现在都包含：

```text
UnityEngine.UI.Button 或其表现子类
GameFramework.UIButton
```

暂时不要配置 `Click Sound`。先验证按钮的点击、状态和动画，之后再接音频。

### 4. 清理旧的 Unity Button 持久化事件

在 Unity `Button` 组件的 `On Click()` 区域：

1. 删除资源包演示用的旧监听。
2. 保留空的 `On Click()` 列表。
3. 业务点击改由 `UIButton.Clicked` 交给页面 Controller。

这样可以避免同一个按钮同时执行 Inspector 旧事件和 Controller 事件。

## 五、页面 Controller 如何使用按钮

下面是一个页面内的最小完整示例。它符合当前项目的分层方式：页面接收表现事件，业务状态变化交给 Command 或 System；页面不直接在 Prefab 上写业务持久化事件。

```csharp
using UnityEngine;

namespace GameFramework.Examples
{
    /// <summary>
    /// 演示页面如何订阅框架按钮。
    /// 真实项目中把 OnConfirmClicked 替换为发送 Command 的代码。
    /// </summary>
    public sealed class FrameworkButtonDemoPage : UIPage, IController
    {
        [SerializeField]
        private UIButton mConfirmButton;

        protected override void Awake()
        {
            base.Awake();

            // Inspector 引用只保存组件关系；按钮事件在页面生命周期内注册。
            mConfirmButton.Clicked += OnConfirmClicked;
            mConfirmButton.SoundRequested += OnButtonSoundRequested;
        }

        private void OnDestroy()
        {
            // 页面销毁前解除订阅，避免页面被重新创建时重复注册。
            if (mConfirmButton == null)
            {
                return;
            }

            mConfirmButton.Clicked -= OnConfirmClicked;
            mConfirmButton.SoundRequested -= OnButtonSoundRequested;
        }

        private void OnConfirmClicked()
        {
            // 网络请求开始前锁定按钮。
            if (!mConfirmButton.TryBeginBusy())
            {
                return;
            }

            // 教学示例先直接结束 busy。
            // 实际项目中应在请求成功、失败、超时或取消的所有分支调用 EndBusy。
            mConfirmButton.EndBusy();
        }

        private void OnButtonSoundRequested(AudioCueKey key)
        {
            // 按钮不持有 AudioService；声音由架构中的 Utility 统一播放。
            this.GetUtility<IAudioService>().Play(key);
        }

        IArchitecture IBelongArchitecture.GetArchitecture()
        {
            return ExampleArchitecture.Interface;
        }
    }
}
```

注意：`mConfirmButton` 必须拖入带有 `UIButton` 的根对象。没有绑定时，页面会在 `Awake` 访问空引用；这是 Prefab 配置错误，应在 Inspector 中修复，不要在每个页面里复制一套按钮查找逻辑。

## 六、配置点击音效

### 1. 先确认音频组已加载

`IAudioService.Play(AudioCueKey key)` 只播放已经加载的音频组。页面打开前，需要由音频初始化流程调用：

```csharp
this.GetUtility<IAudioService>().LoadGroupAsync(
    "UI",
    loaded => Debug.Log($"UI 音频组加载结果：{loaded}"));
```

音频组的字符串必须和当前音频配置一致。不要把资源包的 `AudioClip` 直接拖到 `UIButton`，否则会绕过统一加载、热更新和 Mixer 路径。

### 2. 在 `UIButton` Inspector 配置 Key

在下面两个已迁移 Prefab 的 `UIButton` 组件中按需填写：

```text
Click Sound > Group = UI
Click Sound > Cue   = ButtonConfirm
```

如果 `Group` 或 `Cue` 为空，`SoundRequested` 不会触发，按钮仍然可以正常点击。这就是“无音效按钮”的配置方式，不需要额外的 `mPlayClickSound` 布尔字段。

### 3. 避免双重播放

最终的框架按钮应该满足：

```text
UIButton.SoundRequested -> IAudioService.Play
```

同时满足：

```text
没有 ButtonSounds
没有 AudioSource
没有 CleanButton 的声音回调
```

否则一次点击可能同时播放资源包声音和框架声音。

## 七、后续验收脚本（暂不执行）

为了验证 busy 状态，不要修改正式页面。可以临时新建：

```text
Assets/Framework/Tests/Manual/UIButtonManualCheck.cs
```

把它挂到临时 Canvas 对象上，并把已迁移按钮拖到 `Button` 字段。

```csharp
using System.Collections;
using UnityEngine;

namespace GameFramework.Tests
{
    /// <summary>
    /// 临时验证 UIButton 的点击和 busy 生命周期。
    /// 验证完成后可以删除这个脚本和临时对象。
    /// </summary>
    public sealed class UIButtonManualCheck : MonoBehaviour
    {
        [SerializeField]
        private UIButton mButton;

        private void OnEnable()
        {
            mButton.Clicked += OnClicked;
        }

        private void OnDisable()
        {
            mButton.Clicked -= OnClicked;
        }

        private void OnClicked()
        {
            Debug.Log("UIButton 点击事件已收到。");
            StartCoroutine(UnlockAfterDelay());
        }

        private IEnumerator UnlockAfterDelay()
        {
            if (!mButton.TryBeginBusy())
            {
                Debug.LogWarning("按钮没有进入 busy，可能仍处于不可交互状态。");
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
            mButton.EndBusy();
            Debug.Log("UIButton busy 已结束。");
        }
    }
}
```

验证顺序：

1. 点击一次，Console 出现“点击事件已收到”。
2. 一秒内重复点击，只有第一次进入 Controller。
3. 一秒后按钮恢复可用。
4. 如果配置了 `AudioCueKey`，一次点击只出现一次 UI 音效。
5. 鼠标悬停和按下时，Concave 原有 Animator 仍然正常变化。

## 八、完成标准和下一步

### 本次完成标准

- `Button - Filled With Border.prefab` 和 `Popup Base.prefab` 保留 Concave 的视觉资源和 Animator。
- 根对象有 Unity `Button` 和框架 `UIButton`。
- `CleanButton`、`ButtonSounds`、`AudioSource` 已移除。
- 页面通过 `Clicked` 接收交互，不依赖 Prefab 旧的业务事件。
- 点击音效通过 `AudioCueKey -> IAudioService` 播放。
- busy 状态能阻止网络请求期间的重复点击。
- Concave Prefab 批量检查没有 Missing Script。

### 下一步

完成这个基础按钮后，再按同样方式接入第二个组件：

```text
Tooltip -> 统一显示数据和生命周期
```

暂时不要同时改 Tabs、列表、拖拽和分页。按钮先形成“Prefab 表现 + 框架组件 + Controller 事件 + 音频服务”的完整闭环，后续组件才能复用同一套边界。

