using UnityEngine;

namespace GameFramework.Examples
{
    public sealed class InventoryDemoEntry : MonoBehaviour, IController
    {
        [SerializeField] private UGUIRoot mRoot;
        [SerializeField] private UIButton mOpen;

        private void Start()
        {
            this.GetUtility<IUGUIService>().BindRoot(mRoot);
            mOpen.Clicked += Open;
            Open();
        }

        private void Open() => this.GetUtility<IUGUIService>().OpenAsync<InventoryDemoPage>(
            _ => { }, reason => Debug.LogError(reason, this));

        private void OnDestroy() => mOpen.Clicked -= Open;
        IArchitecture IBelongArchitecture.GetArchitecture() => ExampleArchitecture.Interface;
    }
}
