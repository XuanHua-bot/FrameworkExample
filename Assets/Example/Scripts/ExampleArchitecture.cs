using GameFramework;

namespace GameFramework.Examples
{
    /// <summary>
    /// 示例项目自己的 Architecture。
    /// 框架只提供通用能力，具体项目负责决定注册哪些 Utility、Model 和 System。
    /// </summary>
    public sealed class ExampleArchitecture : Architecture<ExampleArchitecture>
    {
        protected override void Init()
        {
            RegisterModel(new InventoryDemoModel());
            RegisterSystem<IInventoryDemoSystem>(new LocalInventoryDemoSystem());
            RegisterModel(new ShopDemoModel());
            RegisterSystem<IShopDemoSystem>(new LocalShopDemoSystem());
            // UIService 依赖 AssetLoader，所以必须先注册资源加载器。
            RegisterUtility<IAssetLoader>(new ResourcesAssetLoader());
            RegisterUtility<IAudioService>(new AudioService(GetUtility<IAssetLoader>()));
            RegisterUtility<ITimerService>(new TimerService());
            RegisterUtility<IUGUIService>(new UGUIService(GetUtility<IAssetLoader>(), GetUtility<IAudioService>()));
            RegisterUtility<IUINavigationService>(new UINavigationService(GetUtility<IUGUIService>()));
        }
    }
}
