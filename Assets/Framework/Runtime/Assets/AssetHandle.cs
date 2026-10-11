using System;
using Object = UnityEngine.Object;

namespace GameFramework
{
    public sealed class AssetHandle<T> where T : Object // 一个 Handle 表示调用方对资源的一次持有
    {
        //由创建handle的loader传入 , 调用release 时 会回调loader减少引用次数
        private readonly Action<string> mRelease;
        //handle自己记住是否归还过, 防止同一个调用方重复release
        private bool mReleased;

        internal AssetHandle(string key, T asset, Action<string> release)
        {
            //internal业务层只能从iAssetloader得到handle
            Key = key;
            Asset = asset;
            mRelease = release;

        }
        public string Key { get; }//当前handle 对应的资源
        public T Asset { get; }//实际使用的unity资源
        public bool IsReleased => mReleased;//用于调试调用方是否已经归还这次资源持有

        public void Release()
        {
            if (mReleased)return;
            mReleased = true;
            mRelease?.Invoke(Key);
        }
    }
}
