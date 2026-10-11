using System;

namespace GameFramework
{
    
    [AttributeUsage(AttributeTargets.Class,Inherited = true)]//通过反射 读取页面配置
    public class UIPageAttribute:Attribute
    {
        //交给IAssetLoader的Resource相对路径， 例如UI/SettingsPage
        public string ResourceKey { get; }
        //服务再实例化之前使用它决定是否合并重复的打开请求
        public UIOpenMode OpenMode { get; }

        public UILayer Layer { get; }
        
        public UILifetime Lifetime { get; }

        

        public UIPageAttribute(
            string resourceKey,
            UIOpenMode openMode,
            UILayer layer = UILayer.Normal,
            UILifetime lifetime = UILifetime.Scene
            )
        {
            ResourceKey = resourceKey;
            OpenMode = openMode;
            Layer = layer;
            Lifetime = lifetime;
        }
    }   
}
