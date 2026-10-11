using UnityEngine;

namespace GameFramework.Examples
{
    public sealed class ShopDemoEntry : MonoBehaviour, IController
    {
        [SerializeField] private UIButton mOpen;
        private void Start() => mOpen.Clicked += Open;
        private void Open() => this.GetUtility<IUGUIService>().OpenAsync<ShopDemoPage>(
            _ => { }, reason => Debug.LogError(reason, this));
        private void OnDestroy() => mOpen.Clicked -= Open;
        IArchitecture IBelongArchitecture.GetArchitecture() => ExampleArchitecture.Interface;
    }
}
