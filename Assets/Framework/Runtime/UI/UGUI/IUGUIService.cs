using System;
using Framework.Runtime.UI;
using UnityEngine;

namespace GameFramework
{
    public interface IUGUIService:IUtility
    {
        /// <summary> 将当前场景实际存在的UGUIROOT 交给Service</summary>
        void BindRoot(UGUIRoot root);
        void CreateAsync<T>(Action<T> onCreated, Action<string> onFailed = null) where T : UIPage;
        /// <summary> 显示已经由本服务管理的页面 </summary>
        void Show(UIPage page);
        /// <summary> 隐藏已经由本服务管理的页面 </summary>
        void Hide(UIPage page);
        
        /// <summary>
        /// 异步加载 key指向的prefab 并在parent下创建T页面的实例
        /// </summary>
        /// <param name="parent">ui容器节点 父节点</param>
        /// <param name="onOpened">成功后交回已经创建的uiPage实例</param>
        /// <param name="onFailed">加载失败或者prefab配置错误时 返回原因</param>
        /// <typeparam name="T"></typeparam>
        void OpenAsync<T>(
            Action<T> onOpened, 
            Action<String> onFailed = null) where T : UIPage;
        void OpenAsync<T,TData>(
            TData data,
            Action<T> onOpened, 
            Action<String> onFailed = null) 
            where T : UIPage,IUIPageDataReceiver<TData> 
            where TData:class,IUIData;

        bool Close<T>() where T : UIPage;//单例ui 使用
        bool Close(UIPage page);//普通 可重复ui使用
        /// <summary> 退出会话 或者 关闭当前服务管理的全部实例 </summary>
        void CloseAll();
        /// <summary> 创建页面但先保持隐藏, 只调用OnOpen </summary>
        
        /// <summary> 页面完成关闭流程后通知导航服务清理历史记录 </summary>
        event Action<UIPage> pageClosed;
    }
}
