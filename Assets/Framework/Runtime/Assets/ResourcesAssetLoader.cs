using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameFramework
{
    //负责
    //根据路径加载资源
    //合并同一个资源的请求
    //计算资源引用次数
    //在handle释放后减少引用
    public sealed class ResourcesAssetLoader : IAssetLoader, IDeinit
    {
        /// <summary>
        /// 资源缓存条目, 避免重复加载, 管理生命周期(持有实际的引用次数)
        /// </summary>
        private sealed class  AssetEntry
        {
            //同一个key在loader中只缓存一份unity资源对象
            public Object Asset;
            //当前还有多少有效的handle正在使用这份资源，每发出一个有效handle ++ ,没返还一个handle --
            public int RefCount;
            //异步请求时为true , 资源完成后为false
            public bool IsLoading;
            //通知异步等待者时，Release 不能提前从 mEntries 移除当前 Entry。
            public bool IsNotifying;
            //同一份资源加载时  后来的请求 加入这里 避免重复发起loadAsync,资源加载结束后 会各自创建对应类型的 handle
            public readonly List<Action<Object>> OnLoaded = new List<Action<Object>>();
            //异步加载失败时,需要通知全部等待者
            public readonly List<Action<String>> OnFailed = new List<Action<string>>();
        }
        /// <summary>
        /// key 为资源路径,value为资源缓存条目 保存资源和它的资源持有状态
        /// </summary>
        private readonly Dictionary<string,AssetEntry> mEntries = new Dictionary<string, AssetEntry>();

        private AssetLoaderDriver mDriver;//LoadAsync 的 携程宿主
        private bool mDeinited;//框架结束后 拒绝新的加载请求， 阻止就写成继续通知业务
        
        
        
        
        
        public AssetHandle<T> Load<T>(string key) where T : Object
        {
            if (!IsValidKey(key) || mDeinited) return null;
            
            //加载已有资源
            if (mEntries.TryGetValue(key, out var entry))
            {
                if (entry.IsLoading) return null;
                return CreateHandle<T>(key, entry);//增加一次新的持有记录
            }
            
            
            //加载新资源
            var asset = Resources.Load<T>(key);
            if (asset == null) return null;
            //建立缓存entry,在把refcount +1
            entry = new AssetEntry
            {
                Asset = asset,

            };
            mEntries.Add(key, entry);
            return CreateHandle<T>(key, entry);

        }

        public void LoadAsync<T>(string key, Action<AssetHandle<T>> onLoaded, Action<string> onFailed = null) where T : Object
        {
            if (onLoaded ==null) throw new ArgumentNullException(nameof(onLoaded));
            if (!IsValidKey(key))
            {
                onFailed?.Invoke("Asset key is empty");
                return;
            }

            if (mDeinited)
            {
                onFailed?.Invoke("Asset loader is deinited");
                return;
            }

            if (mEntries.TryGetValue(key,out var entry))
            {
                if (entry.IsLoading)
                {
                    AddListener(key, entry, onLoaded, onFailed);
                    return;
                }
                //缓存已经就绪 本帧直接回调 调用方仍然拿到独立handle
                InvokeLoaded(key,entry, onLoaded,onFailed);
                return;
            }

            entry = new AssetEntry { IsLoading = true };
            mEntries.Add(key, entry);
            AddListener(key, entry, onLoaded, onFailed);
            Driver.StartCoroutine(LoadCoroutine(key, entry));
        }

        public void ReleaseAll()
        {
            // 只清除当前 Loader 的缓存记录；服务本身仍可以继续加载。
            // 正在加载的协程恢复后会发现 Entry 已不存在，然后自行退出。
            mEntries.Clear();
        }

        private AssetLoaderDriver Driver
        {
            get
            {
                if (mDriver == null)
                    mDriver = AssetLoaderDriver.Create();
                return mDriver;
            }
        }

        private static bool IsValidKey(string key)
        {
            //Resources 路径不能是null 空白字符等
            return  !string.IsNullOrWhiteSpace(key);
        }

        private AssetHandle<T> CreateHandle<T>(string key, AssetEntry entry) where T : Object
        {
            var asset = entry.Asset as T;
            if (asset == null) return null;
            
            //每次成功交给调用方一个handle 
            entry.RefCount++;
            return new AssetHandle<T>(key, asset, Release);
        }

        private void Release(string key)//key 为资源路径
        {
            //entry 不存在说明已经被relaease 清空 ，loading阶段则还没有handle可以归还
            if (!mEntries.TryGetValue(key, out var entry) || entry.IsLoading) return;
            //Assethandle 已处理统一 handle的重复rease ,这里仍避免计数变为负数
            if (entry.RefCount > 0) entry.RefCount--;
            if (entry.RefCount == 0 && !entry.IsNotifying)
            {
                mEntries.Remove(key); // 移除缓存记录，等待 Unity 在合适时机回收。
            }
            
        }

        private void AddListener<T>(string key, AssetEntry entry, Action<AssetHandle<T>> onLoaded,
            Action<string> onFailed) where T : Object
        {
            //不在此立刻创建handle 因为资源还没有加载完成 等完成后 每个监听者都会通过InvokeLoaded 创建自己的Handle 
             entry.OnLoaded.Add(asset=>InvokeLoaded(key,entry,onLoaded,onFailed));
             //即使onFailed为null 也可以保存； 通知时会通过.?invoke 来跳过他
             entry.OnFailed.Add(onFailed);
        }

        private void InvokeLoaded<T>(string key, AssetEntry entry, Action<AssetHandle<T>> onLoaded, Action<string> onFailed) where T : Object
        {
            //这里创建的是本次请求的 自己的handle ,不会服用其他请求拿到的handle 
            var handle = CreateHandle<T>(key, entry);
            if (handle ==null)
            {
                if (entry.RefCount==0 &&
                    mEntries.TryGetValue(key,out var currentEntry)&&
                    ReferenceEquals(currentEntry,entry))
                {
                    mEntries.Remove(key);
                }
                onFailed?.Invoke($"Asset {key} is not {typeof(T).Name}");
                return;
            }
            onLoaded?.Invoke(handle);
        }

        private IEnumerator LoadCoroutine(string key, AssetEntry entry)
        {
            //这里不传入泛型类型 让同key的不同类型请求共享一次底层加载
            var request = Resources.LoadAsync(key);
            //yield 后unity 会在 资源准备完成的帧继续执行下面的代码
            yield return request;
            //Deinit 或ReleaseAll 可能已经清理的当前Entry
            if (mDeinited|| !mEntries.TryGetValue(key,out var currentEntry)||!ReferenceEquals(currentEntry,entry))yield break;
            entry.IsLoading = false;
            entry.Asset = request.asset;
            if (entry.Asset ==null)
            {
                mEntries.Remove(key);
                NotifyFailed(entry,$"Asset {key} is not found");
                yield break;
            }

            NotifyLoaded(key, entry);

        }

        private void NotifyLoaded(string key, AssetEntry entry)
        {
            //先复制再清空。 回调里 可能再次的loadAsync Release 
            //直接foreach yuanlist可能会造成  集合在枚举时被修改
            var callbacks = entry.OnLoaded.ToArray();
            entry.OnLoaded.Clear();
            entry.OnFailed.Clear();
            // 每个回调内部都会按自己的泛型 T 创建 Handle 并增加 RefCount。
            // 回调中可能立刻 Release；要等全部等待者拿到 Handle 后才可以移除 Entry。
            entry.IsNotifying = true;
            try
            {
                foreach (var callback in callbacks)
                {
                    callback?.Invoke(entry.Asset);
                }
            }
            finally
            {
                entry.IsNotifying = false;

                if (entry.RefCount == 0 &&
                    mEntries.TryGetValue(key, out var currentEntry) &&
                    ReferenceEquals(currentEntry, entry))
                {
                    mEntries.Remove(key);
                }
            }
        }

        private static void NotifyFailed(AssetEntry entry, string message)
        {
            //失败时 也先赋值 和成功回调相同
            var callbacks = entry.OnFailed.ToArray();
            entry.OnLoaded.Clear();
            entry.OnFailed.Clear();
            //资源不存在时 不会创建任何的handle 因此也不会增加refcount
            foreach (var callback in callbacks)
            {
                callback?.Invoke(message);
            }

        }
        
        private sealed class  AssetLoaderDriver : MonoBehaviour//让普通c# utillit 也能使用unity携程
        {
            public static AssetLoaderDriver Create()
            {
                //让加载过程 跨场景继续， 真正的销毁时机由  ResourceAssetLoader.Deinit决定
                var gameObject = new GameObject("[GameFramework]AssetLoader");
                DontDestroyOnLoad(gameObject);
                return gameObject.AddComponent<AssetLoaderDriver>();
            }

            public void Dispose()
            {
                //先停止携程， 避免Destory延迟到帧末 仍有回调继续在运行
                StopAllCoroutines();
                Destroy(gameObject);
            }
        }


        public void Deinit()
        {
            if (mDeinited)
                return;

            // 先阻止新请求和旧协程回调，再清理缓存与协程宿主。
            mDeinited = true;
            ReleaseAll();

            if (mDriver != null)
            {
                mDriver.Dispose();
                mDriver = null;
            }
        }

        
    }
}
