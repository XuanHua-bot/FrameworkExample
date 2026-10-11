using TMPro;
using UnityEngine;

namespace GameFramework.Examples
{
    // 商店卡片的页面表现，和背包 UIItemView 分开，避免把价格布局塞进通用背包 Item。
    public sealed class ShopItemView : MonoBehaviour
    {
        [SerializeField] private UIItemView mItem;
        [SerializeField] private TMP_Text mName;
        [SerializeField] private TMP_Text mPrice;
        [SerializeField] private TMP_Text mStock;

        public void SetContent(ShopDemoProduct product, Sprite icon)
        {
            if (mItem != null) mItem.SetContent(string.Empty, icon, 1);
            if (mName != null) mName.text = product.Name;
            if (mPrice != null) mPrice.text = $"{product.Price} 金币";
            if (mStock != null) mStock.text = product.Stock > 0 ? $"库存 {product.Stock}" : "售罄";
        }

        private void Awake()
        {
            if (mItem == null) mItem = GetComponent<UIItemView>();
        }
    }
}
