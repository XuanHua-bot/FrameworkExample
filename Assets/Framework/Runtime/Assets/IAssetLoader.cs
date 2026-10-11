using System;
using Object = UnityEngine.Object;


namespace GameFramework
{
    public interface IAssetLoader: IUtility
    {
        //同步获取资源 找不到key或者类型不匹配时返回null  //给调用方 对应handle 包括资源
        AssetHandle<T>Load<T>(string key) where T : Object; 
        // 异步获取资源 onLoaded必填, 成功返回handle 错误回调错误信息 //给调用方 对应handle 包括资源
        void LoadAsync<T>(string key,Action<AssetHandle<T>> onLoaded,Action<string>onFailed=null) where T : Object;
        //清空当前loader的全部缓存记录 只有在切换账号 重置框架 或者 architecture.Deinit时 进行调用
        void ReleaseAll();

    }
}
