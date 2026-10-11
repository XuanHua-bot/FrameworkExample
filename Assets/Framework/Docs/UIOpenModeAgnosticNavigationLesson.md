# 页面打开方式与 Single / Multiple 解耦

## 进度

当前阶段：UI 页面打开入口整理（1/1）。

## 一、先确定结论

开发页面时，不应该先判断页面是 Single 还是 Multiple，再决定调用哪个打开方法。

Single 和 Multiple 是页面自身的实例策略，配置在页面脚本的 UIPageAttribute 中：

~~~csharp
[UIPage("UI/ExampleDetailPage", UIOpenMode.Single, UILayer.Popup)]
~~~

页面调用方只需要根据使用目的选择入口：

~~~text
普通打开页面，不需要返回栈：IUGUIService.OpenAsync<T>
进入页面流程，需要 Back() 返回：IUINavigationService.PushAsync<T>
~~~

调用方不需要写：

~~~csharp
if (页面是 Single)
{
    // ...
}
else
{
    // ...
}
~~~

页面到底复用已有实例，还是创建新实例，应由 UGUIService 根据 UIPageAttribute.OpenMode 处理。

## 二、当前代码的问题

文件：

~~~text
E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\Navigation\UINavigationService.cs
~~~

方法：

~~~csharp
private void PushInternal<T>(...)
~~~

当前方法在调用 UGUIService.CreateAsync<T> 之前，自己执行了：

~~~csharp
if (TryGetPage<T>(out var existingPage))
{
    if (!BackTo<T>())
    {
        onFailed?.Invoke("Failed to return to the existing page");
        return;
    }

    applyData?.Invoke(existingPage);
    onOpened?.Invoke(existingPage);
    return;
}
~~~

这会导致：

~~~text
导航服务按页面类型查找已有页面
        ↓
找到同类型页面就不再调用 CreateAsync
        ↓
Multiple 页面也会被当成同类型唯一页面处理
~~~

真正知道页面是 Single 还是 Multiple 的代码是 UGUIService.CreateAsync<T>。因此本次调整的原则是：

~~~text
UINavigationService 只管理返回栈
UGUIService 只管理页面实例策略
~~~

## 三、本次修改哪些代码

只修改：

~~~text
E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\Navigation\UINavigationService.cs
~~~

不修改：

~~~text
E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\UGUI\UGUIService.cs
E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\UGUI\IUGUIService.cs
E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\Navigation\IUINavigationService.cs
~~~

### 第 1 处：修改 PushInternal<T> 方法体

保留当前方法声明：

~~~csharp
private void PushInternal<T>(
    Action<T> applyData,
    Action<T> onOpened,
    Action<string> onFailed)
    where T : UIPage
~~~

在这个方法中，删除从下面这段开始的整个 if 代码块：

~~~csharp
if (TryGetPage<T>(out var existingPage))
{
    if (!BackTo<T>())
    {
        onFailed?.Invoke("Failed to return to the existing page");
        return;
    }

    applyData?.Invoke(existingPage);
    onOpened?.Invoke(existingPage);
    return;
}
~~~

只删除这段，不要删除前面的 mIsNavigating 检查，也不要删除后面的 CreateAsync 调用。

### 第 2 处：修改 CreateAsync<T> 成功回调

在 mUGUIService.CreateAsync<T> 的成功回调中，找到当前代码：

~~~csharp
mIsNavigating = false;
if (mPages.Count > 0)
{
    mUGUIService.Hide(mPages[mPages.Count - 1]);
}

applyData?.Invoke(page);
mPages.Add(page);
mUGUIService.Show(page);
onOpened?.Invoke(page);
~~~

把这一段替换成：

~~~csharp
mIsNavigating = false;

// CreateAsync 已经根据 UIPageAttribute 决定 page 是新实例还是已有 Single 实例。
var existingIndex = mPages.IndexOf(page);
if (existingIndex >= 0)
{
    // 已有实例时，关闭它上面的页面，回到这个页面实例。
    while (mPages.Count - 1 > existingIndex)
    {
        CloseTopPage();
    }

    // 返回已有页面时，也要接收本次打开传入的数据。
    applyData?.Invoke(page);
    mUGUIService.Show(page);
    onOpened?.Invoke(page);
    return;
}

// 新实例才需要隐藏当前栈顶页面并加入返回栈。
if (mPages.Count > 0)
{
    mUGUIService.Hide(mPages[mPages.Count - 1]);
}

applyData?.Invoke(page);
mPages.Add(page);
mUGUIService.Show(page);
onOpened?.Invoke(page);
~~~

这里的判断依据是页面实例，而不是页面类型：

~~~text
page 已经在 mPages 中：UGUIService 返回了已有 Single 实例
page 不在 mPages 中：UGUIService 创建了新实例
~~~

### 第 3 处：删除不再使用的 TryGetPage<T>

因为 PushInternal<T> 不再自己按类型查找页面，下面的方法将没有调用方：

~~~csharp
private bool TryGetPage<T>(out T page)
    where T : UIPage
~~~

删除这个完整方法即可。

不要删除：

~~~text
Back()
BackTo<T>()
~~~

BackTo<T>() 仍然是对外提供的按页面类型返回功能。

## 四、修改后的调用方式

### 普通打开

~~~csharp
this.GetUtility<IUGUIService>().OpenAsync<ExampleMultiplePage>(
    page => { },
    reason => Debug.LogError(reason));
~~~

这里由 UGUIService 处理 UIOpenMode.Multiple，每次创建新实例。

### 进入页面流程

~~~csharp
this.GetUtility<IUINavigationService>().PushAsync<ExampleDetailPage>(
    page => { },
    reason => Debug.LogError(reason));
~~~

这里由 UINavigationService 管理返回栈，页面是否复用由 UGUIService.CreateAsync<T> 根据特性处理。

调用方不需要判断 UIOpenMode。

## 五、为什么仍然保留两个服务入口

需要区分的是是否进入返回栈，不是页面是否单例：

| 需求 | 调用入口 | 是否进入返回栈 |
| --- | --- | --- |
| 打开提示、HUD、独立弹窗 | IUGUIService.OpenAsync<T> | 否 |
| 打开页面流程的下一页 | IUINavigationService.PushAsync<T> | 是 |
| 返回上一页 | IUINavigationService.Back() | 是 |
| 直接关闭当前 Multiple 实例 | IUGUIService.Close(page) | 否 |

如果所有页面都进入导航服务，Toast、伤害跳字、独立提示也会污染返回栈。两个入口的区别来自页面行为，而不是 Single / Multiple 配置。

## 六、本阶段总结

本阶段解决：

~~~text
调用页面时不再关心 Single / Multiple
~~~

最终职责：

~~~text
UIPageAttribute：声明页面策略
UGUIService：执行页面策略
UINavigationService：管理返回栈
业务页面：只选择普通打开或进入导航流程
~~~

这样页面实例策略不会泄漏到业务代码中。
