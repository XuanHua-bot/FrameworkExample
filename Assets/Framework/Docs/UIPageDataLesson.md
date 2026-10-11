# UI 页面参数与导航服务修正（重写版）

## 进度

当前阶段：UI 页面参数传递（1/1）。

本阶段只处理两件事：

1. 修复 UINavigationService 当前的 Rider 编译错误；
2. 让导航服务可以把数据传给目标页面。

本次不修改 UGUIService，不新增导航栈，不修改已有的 Back、BackTo<T> 和关闭逻辑。

---

## 一、当前错误的真实原因

文件：

E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\Navigation\UINavigationService.cs

当前错误方法中的约束是：

~~~csharp
private void PushInternal<T>(
    Func<object, object> applyData,
    Action<T> onOpened,
    Action<string> onFailed)
    where T : UIPage, IUIPageDataReceiver
~~~

### 1. IUIPageDataReceiver 少了类型参数

项目中的接口是：

~~~csharp
public interface IUIPageDataReceiver<T>
{
    void SetData(T data);
}
~~~

因此不能写：

~~~csharp
IUIPageDataReceiver
~~~

必须指定数据类型，例如：

~~~csharp
IUIPageDataReceiver<ExampleDetailPageData>
~~~

但 PushInternal<T> 同时服务于有数据页面和无数据页面，所以它不应该要求所有页面都实现数据接口。最终这里只保留：

~~~csharp
where T : UIPage
~~~

### 2. `UINavigationService.cs` 中 `PushInternal<T>` 的参数类型错误

脚本：

`E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\Navigation\UINavigationService.cs`

方法：

`UINavigationService.PushInternal<T>(...)`

当前方法声明大约在第 36 行，找到下面这段参数声明：

~~~csharp
private void PushInternal<T>(
    Func<object, object> applyData,
    Action<T> onOpened,
    Action<string> onFailed)
    where T : UIPage, IUIPageDataReceiver
~~~

只修改这个方法的第一个参数和泛型约束，改成：

~~~csharp
private void PushInternal<T>(
    Action<T> applyData,
    Action<T> onOpened,
    Action<string> onFailed)
    where T : UIPage
~~~

applyData 的用途是：

~~~text
接收页面实例
把数据写入页面
不返回结果
~~~

这符合：

~~~csharp
Action<T>
~~~

不符合：

~~~csharp
Func<object, object>
~~~

因此方法参数要改成：

~~~csharp
Action<T> applyData
~~~

下面两行是执行回调，不需要改：

~~~csharp
applyData?.Invoke(existingPage);
applyData?.Invoke(page);
~~~

它们的关系是：

~~~text
Action<T> applyData：声明回调变量的类型
applyData?.Invoke(page)：执行回调
~~~

---

## 二、只修改 UINavigationService 中的三处

文件：

E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\Navigation\UINavigationService.cs

本次不要删除整个方法区域，也不要重新粘贴整个脚本。只修改下面三处。

### 第 1 处：修改 PushInternal<T> 的方法声明

找到方法：

~~~csharp
private void PushInternal<T>(...)
~~~

当前声明：

~~~csharp
private void PushInternal<T>(
    Func<object, object> applyData,
    Action<T> onOpened,
    Action<string> onFailed)
    where T : UIPage, IUIPageDataReceiver
~~~

只把它改成：

~~~csharp
private void PushInternal<T>(
    Action<T> applyData,
    Action<T> onOpened,
    Action<string> onFailed)
    where T : UIPage
~~~

这里改了两点：

1. Func<object, object> applyData 改为 Action<T> applyData；
2. 删除 , IUIPageDataReceiver，保留 where T : UIPage。

注意：这里只修改第 36 行 `PushInternal<T>` 的约束。
第 25 行 `PushAsync<T, TData>` 中的：

~~~csharp
where T : UIPage, IUIPageDataReceiver<TData>
~~~

必须保留，因为这个重载确实需要调用 `page.SetData(data)`。

方法体中的下面两行不要修改：

~~~csharp
applyData?.Invoke(existingPage);
applyData?.Invoke(page);
~~~

### 第 2 处：移动新页面的处理代码

在 PushInternal<T> 的 mUGUIService.CreateAsync<T> 回调中，当前代码类似这样：

~~~csharp
if (mPages.Count > 0)
{
    mUGUIService.Hide(mPages[mPages.Count - 1]);

    applyData?.Invoke(page);
    mPages.Add(page);
    mUGUIService.Show(page);
    onOpened?.Invoke(page);
}
~~~

只保留“隐藏上一页”在 if 内，把后面四行移动到 if 外：

~~~csharp
if (mPages.Count > 0)
{
    mUGUIService.Hide(mPages[mPages.Count - 1]);
}

applyData?.Invoke(page);
mPages.Add(page);
mUGUIService.Show(page);
onOpened?.Invoke(page);
~~~

原因是：第一个页面打开时 mPages.Count 等于 0，但第一个页面仍然必须加入栈并显示。

### 第 3 处：确认两个 PushAsync 调用能够匹配

无参数方法保持：

~~~csharp
PushInternal<T>(null, onOpened, onFailed);
~~~

带参数方法保持：

~~~csharp
PushInternal<T>(page => page.SetData(data), onOpened, onFailed);
~~~

因为 page => page.SetData(data) 是 Action<T>，所以它可以传给新的 applyData 参数。

PushAsync<T>、PushAsync<T, TData>、TryGetPage<T>、Back()、BackTo<T>() 和关闭相关代码都不需要整体替换。

---

## 三、页面数据接口

文件：

E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\UIDataContracts.cs

当前接口保持如下：

~~~csharp
namespace Framework.Runtime.UI
{
    public interface IUIData
    {
    }

    public interface IUIPageDataReceiver<T>
    {
        void SetData(T data);
    }
}
~~~

例如数据类型：

~~~csharp
public sealed class ExampleDetailPageData : IUIData
{
    public int LevelId;
}
~~~

页面实现：

~~~csharp
public sealed class ExampleDetailPage
    : UIPage, IUIPageDataReceiver<ExampleDetailPageData>
{
    public void SetData(ExampleDetailPageData data)
    {
        // 使用 data.LevelId 更新页面内容。
    }
}
~~~

调用：

~~~csharp
var data = new ExampleDetailPageData
{
    LevelId = 3
};

navigation.PushAsync<ExampleDetailPage, ExampleDetailPageData>(
    data,
    page => { },
    reason => Debug.LogError(reason));
~~~

---

## 四、本阶段暂时不要修改的文件

本阶段不要修改：

~~~text
E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\UGUI\UGUIService.cs
E:\unityXiangmu\FrameworkExample\Assets\Framework\Runtime\UI\Navigation\IUINavigationService.cs
E:\unityXiangmu\FrameworkExample\Assets\Example\Scripts\ExampleScenePage.cs
E:\unityXiangmu\FrameworkExample\Assets\Example\Scripts\ExampleDetailPage.cs
~~~

原因：

- UGUIService 已经有对应的带数据打开流程；
- IUINavigationService 已经声明了带数据的 PushAsync；
- 当前先恢复 UINavigationService 的编译正确性；
- 示例页面的数据测试放在框架恢复编译后再接入。

---

## 五、验证

### 1. Rider

确认以下错误消失：

~~~text
IUIPageDataReceiver 需要 1 个类型参数
applyData 不存在
UINavigationService 未实现接口成员
~~~

### 2. 关键声明

PushInternal<T> 必须是：

~~~csharp
private void PushInternal<T>(
    Action<T> applyData,
    Action<T> onOpened,
    Action<string> onFailed)
    where T : UIPage
~~~

不能是：

~~~csharp
where T : UIPage, IUIPageDataReceiver
~~~

### 3. 流程

~~~text
PushAsync
    ↓
PushInternal
    ↓
TryGetPage
    ↓
已有页面：BackTo<T> + applyData
    ↓
没有页面：CreateAsync
    ↓
隐藏上一页
    ↓
applyData
    ↓
加入 mPages
    ↓
Show
~~~

---

## 六、总结

本阶段的核心是把“写入页面数据的动作”作为 Action<T> 传入统一导航流程：

~~~csharp
Action<T> applyData
~~~

表示回调参数类型。

~~~csharp
applyData?.Invoke(page);
~~~

表示执行回调。

两者不是互相替换的代码，而是：

~~~text
声明回调类型
    ↓
传入 page => page.SetData(data)
    ↓
页面创建完成后执行 applyData(page)
~~~


