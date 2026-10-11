using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 简单动态列表组件（P1 表现组件）
    /// 适用于少量列表项展示（如标签列表、任务条件列表、装备部位列表、简易商城推荐）
    /// 内部自动维护 Prefab 实例化与简易缓存池，支持单选/不选、点击回调与统一数据绑定
    /// </summary>
    [DisallowMultipleComponent]
    public class UISimpleList : MonoBehaviour
    {
        [Header("Template & Container")]
        [Tooltip("列表项预制体或模板节点（若在容器内会自动克隆或复用）")]
        [SerializeField] private UIListItem mItemTemplate;

        [Tooltip("挂载列表项的父容器，留空则为当前 transform")]
        [SerializeField] private Transform mContainer;

        [Header("Selection Mode")]
        [Tooltip("是否启用单选模式")]
        [SerializeField] private bool mEnableSelection = true;

        [Tooltip("是否允许点击已选中项时取消选中")]
        [SerializeField] private bool mAllowSwitchOff = false;

        // 内部活跃项与隐藏缓存池
        private readonly List<UIListItem> mActiveItems = new List<UIListItem>();
        private readonly Stack<UIListItem> mItemPool = new Stack<UIListItem>();
        private IList mCurrentDataList;
        private int mSelectedIndex = -1;

        public IReadOnlyList<UIListItem> ActiveItems => mActiveItems;
        public int SelectedIndex => mSelectedIndex;
        public UIListItem SelectedItem => (mSelectedIndex >= 0 && mSelectedIndex < mActiveItems.Count) ? mActiveItems[mSelectedIndex] : null;

        /// <summary>
        /// 列表项点击事件 (item, isUserAction)
        /// </summary>
        public event Action<UIListItem, bool> ItemClicked;

        /// <summary>每次数据绑定后填写条目表现，包括滚动复用和强制刷新。</summary>
        public event Action<UIListItem> ItemBound;

        /// <summary>
        /// 列表项选中切换事件 (selectedIndex, oldIndex, isUserAction)
        /// </summary>
        public event Action<int, int, bool> SelectionChanged;

        private bool mInitialized;

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (mInitialized) return;
            mInitialized = true;
            if (mContainer == null)
            {
                mContainer = transform;
            }

            if (mItemTemplate != null && mItemTemplate.gameObject.scene.IsValid())
            {
                // 如果模板是场景中的子节点，初始将其隐藏
                mItemTemplate.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 设置列表数据源并刷新显示
        /// </summary>
        /// <typeparam name="T">数据元素类型</typeparam>
        public void SetData<T>(IList<T> dataList)
        {
            EnsureInitialized();
            mCurrentDataList = (IList)dataList;
            int count = dataList != null ? dataList.Count : 0;

            // 1. 回收多余的活跃项
            while (mActiveItems.Count > count)
            {
                int lastIdx = mActiveItems.Count - 1;
                var item = mActiveItems[lastIdx];
                mActiveItems.RemoveAt(lastIdx);
                RecycleItem(item);
            }

            // 2. 复用或实例化所需项
            for (int i = 0; i < count; i++)
            {
                UIListItem item;
                if (i < mActiveItems.Count)
                {
                    item = mActiveItems[i];
                }
                else
                {
                    item = GetOrCreateItem();
                    mActiveItems.Add(item);
                }

                item.gameObject.SetActive(true);
                item.transform.SetAsLastSibling();
                item.Index = i;
                item.BindData(dataList[i], i);
                ItemBound?.Invoke(item);
                item.SetSelected(i == mSelectedIndex, isUserAction: false);
            }

            // 越界保护
            if (mSelectedIndex >= count)
            {
                mSelectedIndex = -1;
            }
        }

        /// <summary>
        /// 选中指定索引
        /// </summary>
        public bool SelectByIndex(int index, bool isUserAction = false, bool notify = true)
        {
            if (!mEnableSelection) return false;

            if (index < 0 || index >= mActiveItems.Count)
            {
                if (mAllowSwitchOff && index == -1)
                {
                    ClearSelection(isUserAction, notify);
                    return true;
                }
                return false;
            }

            if (mSelectedIndex == index) return false;

            int oldIndex = mSelectedIndex;
            mSelectedIndex = index;

            for (int i = 0; i < mActiveItems.Count; i++)
            {
                mActiveItems[i].SetSelected(i == index, isUserAction);
            }

            if (notify)
            {
                SelectionChanged?.Invoke(mSelectedIndex, oldIndex, isUserAction);
            }

            return true;
        }

        /// <summary>
        /// 清除当前选中
        /// </summary>
        public void ClearSelection(bool isUserAction = false, bool notify = true)
        {
            if (mSelectedIndex == -1) return;

            int oldIndex = mSelectedIndex;
            mSelectedIndex = -1;

            for (int i = 0; i < mActiveItems.Count; i++)
            {
                mActiveItems[i].SetSelected(false, isUserAction);
            }

            if (notify)
            {
                SelectionChanged?.Invoke(-1, oldIndex, isUserAction);
            }
        }

        private void HandleItemClicked(UIListItem item, bool isUserAction)
        {
            ItemClicked?.Invoke(item, isUserAction);

            if (!mEnableSelection || item == null) return;

            int clickedIndex = item.Index;
            if (mSelectedIndex == clickedIndex)
            {
                if (mAllowSwitchOff)
                {
                    ClearSelection(isUserAction, notify: true);
                }
            }
            else
            {
                SelectByIndex(clickedIndex, isUserAction, notify: true);
            }
        }

        private UIListItem GetOrCreateItem()
        {
            UIListItem item;
            if (mItemPool.Count > 0)
            {
                item = mItemPool.Pop();
            }
            else
            {
                if (mItemTemplate == null)
                {
                    Debug.LogError("[UISimpleList] Item template is missing!", this);
                    var fallbackGo = new GameObject("FallbackItem");
                    fallbackGo.transform.SetParent(mContainer, false);
                    item = fallbackGo.AddComponent<UIListItem>();
                }
                else
                {
                    var go = Instantiate(mItemTemplate.gameObject, mContainer);
                    item = go.GetComponent<UIListItem>();
                }
            }

            item.Clicked -= HandleItemClicked;
            item.Clicked += HandleItemClicked;
            return item;
        }

        private void RecycleItem(UIListItem item)
        {
            if (item == null) return;

            item.Clicked -= HandleItemClicked;
            item.ResetForPool();
            item.gameObject.SetActive(false);
            mItemPool.Push(item);
        }

        /// <summary>
        /// 清空并回收所有项
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < mActiveItems.Count; i++)
            {
                RecycleItem(mActiveItems[i]);
            }
            mActiveItems.Clear();
            mSelectedIndex = -1;
            mCurrentDataList = null;
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            Clear();
            ItemClicked = null;
            ItemBound = null;
            SelectionChanged = null;
        }

        private void OnDestroy()
        {
            Clear();
            ItemClicked = null;
            ItemBound = null;
            SelectionChanged = null;
        }
    }
}
