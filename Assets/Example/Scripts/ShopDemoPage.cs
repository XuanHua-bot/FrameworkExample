using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GameFramework.Examples
{
    [UIPage("UI/ShopDemoPage", UIOpenMode.Single, UILayer.Normal, UILifetime.Scene)]
    public sealed class ShopDemoPage : UIPage, IController
    {
        [SerializeField] private UIVirtualList mList;
        [SerializeField] private UITabGroup mTabs;
        [SerializeField] private UIInputField mSearch;
        [SerializeField] private UIBusyState mBusy;
        [SerializeField] private UIButton mBuy;
        [SerializeField] private UIButton mClose;
        [SerializeField] private TMP_Text mDetails;
        [SerializeField] private TMP_Text mBalance;
        [SerializeField] private TMP_Text mResult;
        [SerializeField] private Sprite[] mIcons;

        private readonly List<ShopDemoProduct> mVisible = new List<ShopDemoProduct>();
        private IUnRegister mSubscription;
        private ShopDemoCategory mCategory;
        private string mQuery = string.Empty;
        private int mSelectedId = -1;
        private bool mPending;
        private bool mPurchaseResultUnknown;

        public override void OnOpen()
        {
            mList.ItemBound += BindItem;
            mList.ItemClicked += OpenDetails;
            mList.SelectionChanged += SelectItem;
            mTabs.TabChanged += ChangeCategory;
            mSearch.ValueChanged += Search;
            mBuy.Clicked += Buy;
            mClose.Clicked += Close;
            mBusy.TimeoutTriggered += OnPurchaseTimedOut;
            mSubscription = this.RegisterEvent<ShopDemoChanged>(_ => Refresh());
            Refresh();
        }

        private void BindItem(UIListItem item)
        {
            var product = (ShopDemoProduct)item.RawData;
            item.GetComponent<ShopItemView>().SetContent(product, mIcons[product.IconIndex]);
        }

        private void Refresh()
        {
            mVisible.Clear();
            foreach (var product in this.GetModel<ShopDemoModel>().Snapshot)
                if ((mCategory == ShopDemoCategory.All || mCategory == product.Category) &&
                    (mQuery.Length == 0 || product.Name.IndexOf(mQuery, System.StringComparison.OrdinalIgnoreCase) >= 0))
                    mVisible.Add(product);
            mList.ClearSelection(notify: false);
            mList.SetData(mVisible);
            var index = mVisible.FindIndex(value => value.Id == mSelectedId);
            if (index >= 0) mList.SelectByIndex(index, notify: false);
            else mSelectedId = -1;
            mBalance.text = $"金币：{this.GetModel<ShopDemoModel>().Balance}";
            ShowDetails();
        }

        private void SelectItem(int index, int oldIndex, bool user)
        {
            mSelectedId = index < 0 ? -1 : mVisible[index].Id;
            ShowDetails();
        }

        private void OpenDetails(UIListItem item, bool user)
        {
            if (!user || !(item.RawData is ShopDemoProduct product)) return;
            var data = new ShopDetailData(product.Name, product.Price, product.Stock, mIcons[product.IconIndex]);
            this.GetUtility<IUGUIService>().OpenAsync<ShopDetailPage, ShopDetailData>(
                data, _ => { }, reason => Debug.LogError(reason, this));
        }

        private void ShowDetails()
        {
            var product = mVisible.Find(value => value.Id == mSelectedId);
            mDetails.text = product == null ? "选择商品查看详情" :
                $"{product.Name}\n价格：{product.Price} 金币\n库存：{product.Stock}";
            mBuy.SetInteractable(product != null && product.Stock > 0 && !mPending && !mPurchaseResultUnknown);
        }

        private void ChangeCategory(int index, int oldIndex, bool user)
        {
            mCategory = (ShopDemoCategory)index;
            Refresh();
        }

        private void Search(string value) { mQuery = value ?? string.Empty; Refresh(); }

        private void Buy()
        {
            if (mPending || mPurchaseResultUnknown || mSelectedId < 0) return;
            mPending = true;
            mBuy.SetInteractable(false);
            mBusy.BeginBusy("正在购买...");
            this.GetSystem<IShopDemoSystem>().Purchase(mSelectedId, OnPurchased);
        }

        private void OnPurchased(ShopPurchaseResult result)
        {
            if (!mPending && !mPurchaseResultUnknown) return;
            mPending = false;
            mPurchaseResultUnknown = false;
            mBusy.EndBusy();
            mResult.text = result.Status == ShopPurchaseStatus.Success ? "购买成功" :
                result.Status == ShopPurchaseStatus.SoldOut ? "商品已售罄" :
                result.Status == ShopPurchaseStatus.InsufficientFunds ? "金币不足" : "商品不存在";
            Refresh();
        }

        private void OnPurchaseTimedOut()
        {
            if (!mPending) return;
            // 超时只代表尚未收到结果，避免在原购买仍可能成功时再次购买。
            mPending = false;
            mPurchaseResultUnknown = true;
            mResult.text = "购买结果未确认，请稍候";
            ShowDetails();
        }

        private void Close() => this.GetUtility<IUGUIService>().Close(this);

        public override void OnClose()
        {
            mPending = false;
            mPurchaseResultUnknown = false;
            mBusy.TimeoutTriggered -= OnPurchaseTimedOut;
            mBusy.ForceEndBusy();
            mSubscription?.UnRegister();
            mList.ItemBound -= BindItem;
            mList.ItemClicked -= OpenDetails;
            mList.SelectionChanged -= SelectItem;
            this.GetUtility<IUGUIService>().Close<ShopDetailPage>();
            mTabs.TabChanged -= ChangeCategory;
            mSearch.ValueChanged -= Search;
            mBuy.Clicked -= Buy;
            mClose.Clicked -= Close;
        }

        IArchitecture IBelongArchitecture.GetArchitecture() => ExampleArchitecture.Interface;
    }
}
