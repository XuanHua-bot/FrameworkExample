# Multiple 页面实例

## 当前模块进度（3/3）

当前示例已经覆盖：

- `Single`：同一页面只保留一个实例。
- `Multiple`：同一个 prefab 可以同时打开多个实例。
- `Scene` / `Persistent`：分别验证场景页面和跨场景页面的生命周期。

## 本次功能

新增 `ExampleMultiplePage`，用于验证连续打开同一个 prefab 后，页面是否能独立关闭。

文件：`Assets/Example/Scripts/ExampleMultiplePage.cs`

```csharp
[UIPage("UI/ExampleMultiplePage", UIOpenMode.Multiple, UILayer.Toast, UILifetime.Scene)]
public sealed class ExampleMultiplePage : UIPage, IController
```

`Multiple` 页面每次调用 `OpenAsync<ExampleMultiplePage>` 都会创建新实例。页面关闭时传入当前实例：

```csharp
this.GetUtility<IUGUIService>().Close(this);
```

因此只会移除当前页面，其他相同 prefab 的实例仍然保留。切换场景时，这些页面会按 `UILifetime.Scene` 被统一清理。

## 使用位置

`Assets/Example/Scripts/ExampleControlPanel.cs` 新增“打开 Multiple 页面”按钮。连续点击后会看到多个独立页面，每个页面都能单独关闭。

页面 prefab 由 `Assets/Example/Editor/ExampleProjectGenerator.cs` 生成到：

`Assets/Example/Resources/UI/ExampleMultiplePage.prefab`

## 小结

现在独立示例已经把 UGUI 服务的四个关键行为放到了真实场景中：单例页面、多实例页面、场景页面和跨场景页面。
