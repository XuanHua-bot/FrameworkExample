# FrameworkExample

这是一个独立的 GameFramework 使用示例项目，不依赖其他工程。
当前示例集中演示 UGUIService、UIPage、组件库弹窗 Prefab 和 UITextAnimator 的组合使用。

## 示例内容

- `ExampleArchitecture`：注册资源加载器、音频服务、计时服务和 UGUI 服务。
- `UITextEffectsDemoBootstrap`：把场景中的 `UGUIRoot` 绑定到当前 Architecture。
- `UITextEffectsDemoEntry`：通过 `IUGUIService` 打开文字效果演示页。
- `UITextEffectsDemoPage`：在组件库弹窗副本中展示 fade、scale、typewriter、shake、float、warning、highlight、任务高亮和地图高亮。

## 运行

打开 `Assets/Example/Scenes/UITextEffectsDemo.unity`，运行场景后页面会通过 `IUGUIService` 自动打开。
演示页使用 `Assets/Example/Resources/UI/UITextEffectsDemoPage.prefab`，它是组件库 `Popup - Size Fitter.prefab` 的示例副本；原始组件库 Prefab 不会被修改。
