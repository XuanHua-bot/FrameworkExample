using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Examples
{
    [UIPage("UI/InventoryDemoPage", UIOpenMode.Single, UILayer.Normal, UILifetime.Scene)]
    public sealed class InventoryDemoPage : UIPage, IController
    {
        [SerializeField] private UIVirtualList mList;
        [SerializeField] private TMP_Text mDetails;
        [SerializeField] private TMP_Text mSummary;
        [SerializeField] private UIButton mUse;
        [SerializeField] private UIButton mFilter;
        [SerializeField] private UIButton mRestore;
        [SerializeField] private UIButton mClose;
        [SerializeField] private Sprite[] mIcons;
        [SerializeField] private UITabGroup mCategoryTabs;
        [SerializeField] private UIInputField mSearch;
        [SerializeField] private UIBusyState mBusy;
        [SerializeField] private UIDropTarget mWeaponSlot;
        [SerializeField] private UIDropTarget mArmorSlot;
        [SerializeField] private UIWindowAnimation mWindowAnimation;
        [SerializeField] private TMP_Text mEquipSummary;
        [SerializeField] private Image mWeaponIcon;
        [SerializeField] private Image mArmorIcon;

        private readonly List<InventoryDemoItem> mVisible = new List<InventoryDemoItem>();
        private IUnRegister mSubscription;
        private int mSelectedId = -1;
        private bool mUsableOnly;
        private InventoryDemoCategory mCategory = InventoryDemoCategory.All;
        private string mSearchText = string.Empty;

        public override void OnOpen()
        {
            mList.ItemBound += PresentItem;
            mList.SelectionChanged += SelectItem;
            mUse.Clicked += UseSelected;
            mFilter.Clicked += ToggleFilter;
            mRestore.Clicked += Restore;
            mClose.Clicked += Close;
            if (mCategoryTabs != null) mCategoryTabs.TabChanged += OnCategoryChanged;
            if (mSearch != null) mSearch.ValueChanged += OnSearchChanged;
            if (mWeaponSlot != null) mWeaponSlot.Dropped += OnEquipmentDropped;
            if (mArmorSlot != null) mArmorSlot.Dropped += OnEquipmentDropped;
            mSubscription = this.RegisterEvent<InventoryDemoChanged>(_ => Refresh());
        }

        public override void OnShow()
        {
            if (mWindowAnimation != null) mWindowAnimation.PlayOpen();
            if (mBusy != null) mBusy.BeginBusy("正在载入背包...");
            StartCoroutine(InitialLoad());
        }

        private IEnumerator InitialLoad()
        {
            yield return new WaitForSecondsRealtime(.25f);
            Refresh();
            if (mBusy != null) mBusy.EndBusy();
        }

        private void PresentItem(UIListItem item)
        {
            var data = (InventoryDemoItem)item.RawData;
            var icon = data.IconIndex < mIcons.Length ? mIcons[data.IconIndex] : null;
            var drag = item.GetComponent<UIDragHandle>();
            if (drag != null) {
                drag.Interactable = data.Equipment != InventoryDemoEquipment.None;
                drag.SetPayload(data);
                drag.SetDragIcon(data.Equipment != InventoryDemoEquipment.None ? icon : null);
            }
            item.GetComponent<UIItemView>().SetContent(data.Name, icon, data.Count, data.Locked);
        }

        private void Refresh()
        {
            mVisible.Clear();
            foreach (var item in this.GetModel<InventoryDemoModel>().Snapshot)
            {
                bool categoryMatches = mCategory == InventoryDemoCategory.All || item.Category == mCategory;
                bool searchMatches = string.IsNullOrEmpty(mSearchText) || item.Name.IndexOf(mSearchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (categoryMatches && searchMatches && (!mUsableOnly || (item.Consumable && !item.Locked))) mVisible.Add(item);
            }

            // 列表索引会随过滤和删除变化，页面按业务 ID 恢复选择。
            mList.ClearSelection(notify: false);
            mList.SetData(mVisible);
            int index = mVisible.FindIndex(item => item.Id == mSelectedId);
            if (index >= 0) mList.SelectByIndex(index, notify: false);
            else mSelectedId = -1;
            mSummary.text = $"{mVisible.Count} / {this.GetModel<InventoryDemoModel>().Snapshot.Count} 件物品 · 虚拟列表";
            mFilter.SetText(mUsableOnly ? "查看全部" : "仅可使用");
            ShowEquipment();
            ShowDetails();
        }

        private void ShowEquipment()
        {
            var model = this.GetModel<InventoryDemoModel>();
            var weapon = model.Items.Find(value => value.Id == model.EquippedWeaponId);
            var armor = model.Items.Find(value => value.Id == model.EquippedArmorId);
            mWeaponIcon.sprite = weapon != null ? mIcons[weapon.IconIndex] : mIcons[2];
            mArmorIcon.sprite = armor != null ? mIcons[armor.IconIndex] : mIcons[5];
            mWeaponIcon.color = new Color(1, 1, 1, weapon == null ? .25f : 1);
            mArmorIcon.color = new Color(1, 1, 1, armor == null ? .25f : 1);
            mEquipSummary.text = weapon == null && armor == null ? "拖动装备到对应槽位" :
                $"武器：{(weapon == null ? "未装备" : weapon.Name)}\n防具：{(armor == null ? "未装备" : armor.Name)}";
        }

        private void SelectItem(int index, int oldIndex, bool isUserAction)
        {
            mSelectedId = index >= 0 ? mVisible[index].Id : -1;
            ShowDetails();
        }

        private void ShowDetails()
        {
            var item = mVisible.Find(value => value.Id == mSelectedId);
            var category = item == null ? "" : item.Category == InventoryDemoCategory.Weapon ? "武器" :
                item.Category == InventoryDemoCategory.Armor ? "防具" :
                item.Category == InventoryDemoCategory.Material ? "材料" : "消耗品";
            mDetails.text = item == null ? "选择物品查看详情" :
                $"{item.Name}\n数量：{item.Count}\n分类：{category}\n状态：{(item.Locked ? "已锁定" : "可用")}\n编号：{item.Id}";
            mUse.SetInteractable(item != null && item.Consumable && !item.Locked);
        }

        private void UseSelected() { if (mBusy != null) mBusy.BeginBusy("正在使用物品..."); StartCoroutine(UseRoutine()); }
        private IEnumerator UseRoutine() { yield return new WaitForSecondsRealtime(.15f); this.SendCommand(new UseInventoryDemoItem(mSelectedId)); if (mBusy != null) mBusy.EndBusy(); }
        private void ToggleFilter() { mUsableOnly = !mUsableOnly; RefreshWithBusy(); }
        private void Restore() => this.SendCommand(new RestoreInventoryDemo());
        private void RefreshWithBusy() { if (mBusy == null) { Refresh(); return; } mBusy.BeginBusy("正在筛选..."); StartCoroutine(FilterRoutine()); }
        private IEnumerator FilterRoutine() { yield return new WaitForSecondsRealtime(.12f); Refresh(); mBusy.EndBusy(); }
        private void OnSearchChanged(string value) { mSearchText = value; RefreshWithBusy(); }
        private void OnCategoryChanged(int index, int oldIndex, bool user) { mCategory = (InventoryDemoCategory)Mathf.Clamp(index, 0, 4); RefreshWithBusy(); }
        private void OnEquipmentDropped(UIDropTarget target, UIDragHandle handle, object payload)
        {
            var item = payload as InventoryDemoItem;
            var expected = target == mWeaponSlot ? InventoryDemoEquipment.Weapon : InventoryDemoEquipment.Armor;
            bool equipped = item != null && this.SendCommand(new EquipInventoryDemoItem(item.Id, expected));
            handle.SetDropResult(equipped);
            if (!equipped)
                mEquipSummary.text = "部位不符或物品已锁定";
        }
        private void Close() { if (mWindowAnimation != null) mWindowAnimation.PlayClose(() => this.GetUtility<IUGUIService>().Close(this)); else this.GetUtility<IUGUIService>().Close(this); }

        public override void OnClose()
        {
            mSubscription?.UnRegister();
            mSubscription = null;
            mList.ItemBound -= PresentItem;
            mList.SelectionChanged -= SelectItem;
            mUse.Clicked -= UseSelected;
            mFilter.Clicked -= ToggleFilter;
            mRestore.Clicked -= Restore;
            mClose.Clicked -= Close;
            if (mCategoryTabs != null) mCategoryTabs.TabChanged -= OnCategoryChanged;
            if (mSearch != null) mSearch.ValueChanged -= OnSearchChanged;
            if (mWeaponSlot != null) mWeaponSlot.Dropped -= OnEquipmentDropped;
            if (mArmorSlot != null) mArmorSlot.Dropped -= OnEquipmentDropped;
        }

        IArchitecture IBelongArchitecture.GetArchitecture() => ExampleArchitecture.Interface;
    }
}
