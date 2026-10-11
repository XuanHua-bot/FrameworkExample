using System;
using System.Collections.Generic;
using Framework.Runtime.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameFramework
{
    /// <summary>
    /// 页面生命周期
    /// 负责 页面配置
    /// 判断 ui 是否为single 或 Multiple
    /// 请求资源加载
    /// 创建页面实例
    /// 保存页面(UI 实例上的 UIPage 组件)和handle(资源路径(字典中作为键),prefab,归还资源的方法)的关系
    /// onopen onclose
    /// 销毁页面
    /// 释放handle
    /// </summary>
    public sealed class UGUIService:IUGUIService,IDeinit
    {
        
        private readonly IAssetLoader mAssetLoader;//构造时传入
        private readonly IAudioService mAudioService;
        private UGUIRoot mRoot;//当前场景绑定的root 实例化时 从root 取得layer的父节点
        /// <summary>
        /// 激活场景每变化一次 就递增，  Scene页面 记录打开时的版本， 资源完成时用来判断请求是否过期
        /// </summary>
        private int mSceneVersion;
        private bool mSceneEventsRegistered;//防止BindRoot 重复订阅ActiveSceneChanged
        
        //---已经创建的实例---
        //场景中的每个页面实例，都保存自己对应的 OpenedPageEntry。
        //OpenedPageEntry 内部保存该实例独立持有的 Handle 和 Lifetime。
        /*<instance1, entry1(handle1, lifetime1)>
        <instance2, entry2(handle2, lifetime2)>
        <instance3, entry3(handle3, lifetime3)>*/
        private readonly Dictionary<UIPage, OpenedPageEntry> mOpenedPages = new Dictionary<UIPage, OpenedPageEntry>();
        //---已经创建完成的Single页面---
        //single页面创建成功后 按controller 类型取得同一份实例，close<T> 和重复Single打开 都查询此表
        private readonly Dictionary<Type, UIPage> mOpenedSinglePages = new Dictionary<Type, UIPage>();
        //---正在加载的Single请求---
        //single的第一位那个球尚未加载完全时，保存其余请求的回调  防止残生多个loadAsync 和多个实例
        private readonly Dictionary<Type, PendingOpen> mOpeningSinglePages = new Dictionary<Type, PendingOpen>();
        //---页面类型对应的配置---
        //getUI的创建方式时 会 创建Attribute对象 ，这里保存防止 重复读取
        private readonly Dictionary<Type, UIPageAttribute> mPageAttributes = new Dictionary<Type, UIPageAttribute>();
        
        private bool mDeinited;
        /// <summary>已登记页面完成关闭流程后通知导航服务</summary>
        public event Action<UIPage> pageClosed; 


        private UIPageAttribute GetPageAttribute(Type pageType)
        {
            if (mPageAttributes.TryGetValue(pageType, out var attribute)) return attribute;
            //读取 具体的uiController 脚本的  特性
            attribute = Attribute.GetCustomAttribute(pageType, typeof(UIPageAttribute)) as UIPageAttribute;

            if (attribute!=null)//缺少特性不进行缓存 会把配置错误交给onfailed
            {
                mPageAttributes.Add(pageType,attribute);
            }

            return attribute;
        }

        private Transform GetParent(UILayer layer, Action<string> onFailed)
        {
            if (mRoot==null)
            {
                onFailed?.Invoke("UGUIRoot is not bound");
                return null;
            }

            var parent = mRoot.GetLayer(layer);
            if (parent==null)
            {
                onFailed?.Invoke("UGUI has no transform bound for uilayer "+layer+".");
                return null;
            }

            return parent;
        }

        /// <summary>
        /// 需要传入 资源加载器，资源加载器可以是其他 加载方式
        /// </summary>
        /// <param name="assetLoader"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public UGUIService(IAssetLoader assetLoader, IAudioService audioService = null)
        {
            mAssetLoader = assetLoader??throw new ArgumentNullException(nameof(assetLoader));
            mAudioService = audioService;
        }

        public void BindRoot(UGUIRoot root)
        {
            if (root ==null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (mRoot!=null && !ReferenceEquals(mRoot,root))
            {
                throw new InvalidOperationException("UGUI already has a bound UGUIRoot.");
            }

            mRoot = root;
            if (!mSceneEventsRegistered)
            {
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
                mSceneEventsRegistered = true;
            }
        }

        public void Show(UIPage page)
        {
            if (page==null||!mOpenedPages.ContainsKey(page))return;
            if (page.gameObject.activeSelf)return;
            
            page.gameObject.SetActive(true);
            page.transform.SetAsLastSibling();
            page.SetInputBlocker(true);
            page.OnShow();
            page.GetComponent<UIAudioBinder>()?.PlayShown();
        }

        public void Hide(UIPage page)
        {
            if (page==null|| !mOpenedPages.ContainsKey(page))return;
            if (!page.gameObject.activeSelf)return;
            page.OnHide();
            page.GetComponent<UIAudioBinder>()?.PlayHidden();
            page.SetInputBlocker(false);
            page.gameObject.SetActive(false);
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            if (mDeinited)
            {
                return;
            }

            mSceneVersion++;
            CloseScenePages();
        }


        public void OpenAsync<T>(Action<T> onOpened, Action<string> onFailed = null) where T : UIPage
        {
            CreateAsync<T>(page =>
            {
                Show(page);
                onOpened?.Invoke(page);
            }, onFailed);
            /**/
            


        }

        public void OpenAsync<T, TData>(TData data, Action<T> onOpened, Action<string> onFailed = null) where T : UIPage, IUIPageDataReceiver<TData> where TData : class, IUIData
        {
            if (data==null)
            {
                onFailed?.Invoke("data is null");
                return;
            }
            CreateAsync<T>(page =>
            {
                //CreateAsync返回时， 页面已经完成Onopen 但尚未Show
                //因此数据先写入， OnShow看到的一定是本次数据
                page.SetData(data);
                Show(page);
                onOpened?.Invoke(page);
            },onFailed);
        }

        public void CreateAsync<T>(Action<T> onCreated, Action<string> onFailed=null) where T : UIPage
        {
            //检查服务状态
            if (mDeinited)
            {
                onFailed?.Invoke("UGUIService has been deinited");
                return;
            }
            //读取配置
            var pageType = typeof(T);
            var attribute = GetPageAttribute(pageType);
            
            
            if (attribute==null)
            {
                onFailed?.Invoke("no UIPageAttribute on type "+pageType.FullName);
                return;
            }
            var lifetime = attribute.Lifetime;
            var sceneVersion = mSceneVersion;//记录 打开请求发起时 的场景版本
            
            //根据配置中的模式分流,如果为single 则进入下一个分支
            if (attribute.OpenMode==UIOpenMode.Multiple)
            {
                var parent = GetParent(attribute.Layer, onFailed);
                if (parent==null)
                {
                    return;
                }
                //multiple不进入PendingOpen 每次请求都独立创建实例
                OpenMultiple(attribute.ResourceKey, parent,lifetime,sceneVersion, onCreated, onFailed);
                return;
            }
            
            //Single 已经穿件完成时, 直接交出现有实例
            //CreateAsync 只负责交出实例, 不负责显示
            if (mOpenedSinglePages.TryGetValue(pageType, out var openedPage))
            {
                onCreated?.Invoke((T)openedPage);
                return;
            }
            //检查 single页面是否正在 加载
            if (mOpeningSinglePages.TryGetValue(pageType,out var pending))
            {
                AddPendingCallbacks(pending,onCreated,onFailed);
                return;
            }
            
            var openParent = GetParent(attribute.Layer, onFailed);
            if (openParent==null)
            {
                return;
            }
            
            //请求资源加载器 加载资源
            pending = new PendingOpen(lifetime);
            mOpeningSinglePages.Add(pageType,pending);
            AddPendingCallbacks(pending,onCreated,onFailed);
            
            mAssetLoader.LoadAsync<GameObject>(attribute.ResourceKey,
                handle=>CompleteSingleOpen<T>(
                    pageType,
                    openParent,
                    lifetime,
                    sceneVersion,
                    handle),
                reason=>FailSingleOpen(pageType,reason));
        }

        private void OpenMultiple<T>(
            string resourcekey, 
            Transform parent,
            UILifetime lifetime,
            int sceneVersion,
            Action<T> onCreated, 
            Action<string> onFailed) where T : UIPage
        {
            LoadAndCreate<T>(resourcekey, parent,lifetime,sceneVersion,(page, handle) =>
            {
                mOpenedPages.Add(page, new OpenedPageEntry(handle, lifetime));
                page.GetComponent<UIAudioBinder>()?.Bind(mAudioService);
                page.OnOpen();
                //onOpen中允许页面主动Close自己
                if (mOpenedPages.ContainsKey(page))
                {
                    onCreated?.Invoke(page);
                }
                else
                {
                    onFailed?.Invoke("UI page was closed during OnOpen.");
                }
                
            }, onFailed);
            
        }

        private void CompleteSingleOpen<T>(
            Type pageType, 
            Transform parent,
            UILifetime lifetime,
            int sceneVersion,
            AssetHandle<GameObject> handle)
            where T : UIPage
        {
            //加载完成后重新检查PendingOpen 来检验  请求加载后的回调是否还有效
            if (!mOpeningSinglePages.TryGetValue(pageType,out var pending))
            {
                //请求已被清理, 当前handle 不能继续持有
                //资源虽然加载完成 但是不再创建页面
                handle.Release();
                return;
            }

            if (lifetime == UILifetime.Scene&& sceneVersion != mSceneVersion)
            {
                mOpeningSinglePages.Remove(pageType);
                handle.Release();
                pending.NotifyFailed("Scene version changed before loading completed");
                return;
            }

            if (mDeinited)
            {
                mOpeningSinglePages.Remove(pageType);
                handle.Release();
                pending.NotifyFailed("UGUIService has been deinited");
                return;
            }

            if (parent==null)
            {
                mOpeningSinglePages.Remove(pageType);
                handle.Release();
                pending.NotifyFailed("UI parent was destroyed before loading completed");
                return;
            }
            
            
            var instance = UnityEngine.Object.Instantiate(handle.Asset, parent);
            var page = instance.GetComponent<T>();
            if (page==null)
            {
                mOpeningSinglePages.Remove(pageType);
                UnityEngine.Object.Destroy(instance);
                handle.Release();
                pending.NotifyFailed("prefab has no component of type"+typeof(T).FullName);
                return;
            }
            //single 页面也先保持隐藏, 当代OpenAsync 或者导航服务show
            instance.SetActive(false);
            instance.transform.SetAsLastSibling();
            //先登记两张表, 再调用onopen, 毁掉内部此时可以安全的Close 或在此Open
            mOpenedPages.Add(page,new OpenedPageEntry(handle, lifetime));//保存 所有页面共有的实例 和Handle关系
            mOpenedSinglePages.Add(pageType,page);//保存Single页面 按类型的关系
            mOpeningSinglePages.Remove(pageType);//移除请求回调（也就是不处于加载中）
            
            page.GetComponent<UIAudioBinder>()?.Bind(mAudioService);
            page.OnOpen();
            //OnOpen主动关闭自己时, 本次创建请求应 报告失败
            if (mOpenedPages.ContainsKey(page))
            {
                pending.NotifyOpened(page);
            }
            else
            {
                pending.NotifyFailed("UI page was closed during OnOpen.");
            }

        }

        

        private void LoadAndCreate<T>(string resourceKey, Transform parent,UILifetime lifetime,int sceneVersion, Action<T, AssetHandle<GameObject>> onCreate,
            Action<string> onFailed) where T : UIPage
        {
            mAssetLoader.LoadAsync<GameObject>(resourceKey, handle =>
            {
                if (mDeinited)
                {
                    handle.Release();
                    onFailed?.Invoke("UGUIService has been deinited");
                    return;
                }

                if (lifetime== UILifetime.Scene&& sceneVersion!=mSceneVersion)
                {
                    handle.Release();
                    onFailed?.Invoke("Scene changed before UI page finished loading");
                    return;
                }
                if (parent==null)
                {
                    handle.Release();
                    onFailed?.Invoke("UI parent was destroyed before loading completed");
                    return;
                }
                var instance = UnityEngine.Object.Instantiate(handle.Asset, parent);
                var page = instance.GetComponent<T>();
                if (page==null)
                {
                    //prefab配置错误时, 实例和本次handle必须 一起清理
                    UnityEngine.Object.Destroy(instance);
                    handle.Release();
                    onFailed?.Invoke("prefab has no component of type"+typeof(T).FullName);
                    return;
                }
                instance.SetActive(false);
                instance.transform.SetAsLastSibling();
                onCreate(page, handle);
            },onFailed);
            
        }
        
        
        

        private void FailSingleOpen(Type pageType, string reason)
        {
            if (!mOpeningSinglePages.TryGetValue(pageType,out var pending))
            {
                return;
            }
            //先移除pending 再通知回调, 回调失败可以立即重新发起打开
            mOpeningSinglePages.Remove(pageType);
            pending.NotifyFailed(reason);
        }

        public bool Close(UIPage page)//传入具体页面(ui实例的继承于uipage 组件)
        {
            // 先查询页面对应的 Handle。
            if (page == null || !mOpenedPages.TryGetValue(page, out var entry))
            {
                return false;
            }

            // 查询成功后，再执行原来的两行。
            Hide(page);
            mOpenedPages.Remove(page);

            var pageType = page.GetType();

            if (mOpenedSinglePages.TryGetValue(pageType, out var openedPage)
                && ReferenceEquals(openedPage, page))
            {
                mOpenedSinglePages.Remove(pageType);
            }

            page.OnClose();
            pageClosed?.Invoke(page);

            UnityEngine.Object.Destroy(page.gameObject);
            entry.Handle.Release();
            return true;
            
        }

        private bool CancelPendingOpen(Type pageType, string reason)
        {
            if (!mOpeningSinglePages.TryGetValue(pageType,out var pending))
            {
                return false;
            }
            mOpeningSinglePages.Remove(pageType);
            pending.NotifyFailed(reason);
            return true;
        }
        //根据类型 关闭页面
        public bool Close<T>() where T : UIPage
        {
            var pageType = typeof(T);
            if ( mOpenedSinglePages.TryGetValue(pageType,out var page))
            {
                return Close(page);
            }

            return CancelPendingOpen(pageType, "ui page open was canceled");

        }

        public void CloseAll()
        {//先复制一份 list防止边便利边 删除
            var pages = new List<UIPage>(mOpenedPages.Keys);
            foreach (var page in pages)
            {
                Close(page);
            }
            
        }

        

        

        private void CloseScenePages()
        {
            var scenePages = new List<UIPage>();
            foreach (var pair in mOpenedPages)
            {
                if (pair.Value.Lifetime== UILifetime.Scene)
                {
                    scenePages.Add(pair.Key);
                }
            }

            foreach (var page in scenePages)
            {
                Close(page);
            }

            var pendingSceneTypes = new List<Type>();
            foreach (var pair in mOpeningSinglePages)
            {
                if (pair.Value.Lifetime== UILifetime.Scene)
                {
                    pendingSceneTypes.Add(pair.Key);
                }
            }

            foreach (var pageType in pendingSceneTypes)
            {
                CancelPendingOpen(pageType, "Scene changed before ui page finished loading");
            }
        }

        public void Deinit()
        {
            if (mDeinited)return;

            mDeinited = true;
            if (mSceneEventsRegistered)
            {
                SceneManager.activeSceneChanged -= OnActiveSceneChanged;
                mSceneEventsRegistered = false;
            }
            CloseAll();

            var pendingOpens = new List<PendingOpen>(mOpeningSinglePages.Values);
            mOpeningSinglePages.Clear();
            foreach (var pending in pendingOpens)//通知所有等待着服务已结束
            {
                pending.NotifyFailed("UGUIService has been deinited");
            }

            mRoot = null;
        }


        private static void AddPendingCallbacks<T>(
            PendingOpen pending,
            Action<T> onOpened,
            Action<string> onFailed) where T : UIPage
        {
            //PendingOpen不知道具体的T 通知时 再把UIPage转回本次请求的T
            pending.OpenedCallbacks.Add(page=>onOpened?.Invoke((T)page));
            //失败原因对同一轮加载的所有等待者相同,直接加入失败回调列表
            pending.FailedCallbacks.Add(onFailed);
        }
        
        private sealed class  OpenedPageEntry//一个页面实例{自己的handle , 自己的lifeTime}
        {
            public readonly AssetHandle<GameObject> Handle;
            public readonly UILifetime Lifetime;

            public OpenedPageEntry(AssetHandle<GameObject> handle, UILifetime lifetime)
            {
                Handle = handle;
                Lifetime=lifetime;
            }
        }
        
        private sealed class PendingOpen
        {
            public readonly UILifetime Lifetime;
            //Single 页面的重复请求共用同一次加载，完成后都收到同一个页面实例。
            public readonly List<Action<UIPage>> OpenedCallbacks = new List<Action<UIPage>>();
            //加载失败时， 所有等待这次加载的调用方都要受到同一个失败原因
            public readonly List<Action<string>> FailedCallbacks = new List<Action<string>>();

            public PendingOpen(UILifetime lifetime)
            {   
                Lifetime = lifetime;
            }
            
            public void NotifyOpened(UIPage page)
            {
                //先赋值， 避免回调内部再次调用OpenAsync 时 修改当前表 
                var callbacks = OpenedCallbacks.ToArray();
                OpenedCallbacks.Clear();
                FailedCallbacks.Clear();

                foreach (var callback in callbacks)
                {
                    callback?.Invoke(page);
                }
            }

            public void NotifyFailed(string reason)
            {
                //失败后清空两类回调 避免失败请求残留到下一次打开
                var callbacks = FailedCallbacks.ToArray();
                OpenedCallbacks.Clear();
                FailedCallbacks.Clear();
                foreach (var callback in callbacks)
                {
                    callback?.Invoke(reason);
                }
            }
        }
    }
    
   
}
