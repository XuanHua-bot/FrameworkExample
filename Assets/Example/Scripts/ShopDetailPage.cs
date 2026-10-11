using Framework.Runtime.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Examples
{
    public sealed class ShopDetailData : IUIData
    {
        public readonly string Name;
        public readonly int Price;
        public readonly int Stock;
        public readonly Sprite Icon;

        public ShopDetailData(string name, int price, int stock, Sprite icon)
        {
            Name = name;
            Price = price;
            Stock = stock;
            Icon = icon;
        }
    }

    [UIPage("UI/ShopDetailPage", UIOpenMode.Single, UILayer.Popup, UILifetime.Scene)]
    public sealed class ShopDetailPage : UIPage, IUIPageDataReceiver<ShopDetailData>, IController
    {
        [SerializeField] private TMP_Text mTitle;
        [SerializeField] private TMP_Text mInfo;
        [SerializeField] private Image mIcon;
        [SerializeField] private UIButton mClose;

        public override void OnOpen() => mClose.Clicked += Close;

        public void SetData(ShopDetailData data)
        {
            mTitle.text = data.Name;
            mInfo.text = $"价格  {data.Price} 金币\n库存  {data.Stock}";
            mIcon.sprite = data.Icon;
        }

        private void Close() => this.GetUtility<IUGUIService>().Close(this);

        public override void OnClose() => mClose.Clicked -= Close;

        IArchitecture IBelongArchitecture.GetArchitecture() => ExampleArchitecture.Interface;
    }
}
