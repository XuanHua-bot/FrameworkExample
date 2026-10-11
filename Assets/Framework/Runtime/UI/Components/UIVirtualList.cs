using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 虚拟滚动列表组件（P1 核心组件）
    /// 支持大量数据（100 ~ 10000+）的高性能滚动渲染
    /// 只为视口（Viewport）内及缓冲区的可见区域创建 Item，滚动时复用对象池，减少长列表创建和销毁开销
    /// 支持垂直/水平单列排布以及网格（Grid）多列排布
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect))]
    public class UIVirtualList : MonoBehaviour
    {
        public enum ListLayout
        {
            Vertical,
            Horizontal,
            VerticalGrid,
            HorizontalGrid
        }

        [Header("Scroll References")]
        [SerializeField] private ScrollRect mScrollRect;
        [SerializeField] private RectTransform mViewport;
        [SerializeField] private RectTransform mContent;

        [Header("Item Template")]
        [Tooltip("列表项预制体或模板节点")]
        [SerializeField] private UIListItem mItemTemplate;

        [Header("Layout Settings")]
        [SerializeField] private ListLayout mLayout = ListLayout.Vertical;
        [SerializeField] private Vector2 mItemSize = new Vector2(100f, 100f);
        [SerializeField] private Vector2 mSpacing = new Vector2(10f, 10f);
        [SerializeField] private RectOffset mPadding;

        [Tooltip("网格模式下的列数（VerticalGrid）或行数（HorizontalGrid）")]
        [SerializeField] private int mConstraintCount = 4;

        [Header("Buffer")]
        [Tooltip("视口外额外渲染的缓冲元素数量，避免高速滑动时出现边缘闪烁空白")]
        [SerializeField] private int mBufferCount = 2;

        [Header("Selection Mode")]
        [SerializeField] private bool mEnableSelection = true;
        [SerializeField] private bool mAllowSwitchOff = false;

        // 内部数据与对象池
        private IList mDataList;
        private int mTotalCount;
        private int mSelectedIndex = -1;

        // 当前正在视口中显示的 Item 字典：Key = 数据索引，Value = UIListItem 实例
        private readonly Dictionary<int, UIListItem> mVisibleItems = new Dictionary<int, UIListItem>();
        private readonly Stack<UIListItem> mItemPool = new Stack<UIListItem>();

        public int SelectedIndex => mSelectedIndex;
        public int TotalCount => mTotalCount;

        /// <summary>
        /// 列表项点击事件 (item, isUserAction)
        /// </summary>
        public event Action<UIListItem, bool> ItemClicked;

        /// <summary>每次数据绑定后填写条目表现，包括滚动复用和强制刷新。</summary>
        public event Action<UIListItem> ItemBound;

        /// <summary>
        /// 选中项变更事件 (selectedIndex, oldIndex, isUserAction)
        /// </summary>
        public event Action<int, int, bool> SelectionChanged;

        private bool mInitialized;

        private void Awake() => EnsureComponents();

        private void OnEnable()
        {
            EnsureComponents();
            if (mScrollRect != null)
            {
                mScrollRect.onValueChanged.AddListener(OnScroll);
            }
        }

        private void OnDisable()
        {
            if (mScrollRect != null)
            {
                mScrollRect.onValueChanged.RemoveListener(OnScroll);
            }
        }

        private void EnsureComponents()
        {
            if (mPadding == null) mPadding = new RectOffset(10, 10, 10, 10);
            if (!mInitialized)
            {
                mInitialized = true;
                if (mItemTemplate != null && mItemTemplate.gameObject.scene.IsValid())
                    mItemTemplate.gameObject.SetActive(false);
            }
            if (mScrollRect == null)
            {
                mScrollRect = GetComponent<ScrollRect>();
            }

            if (mScrollRect != null)
            {
                if (mViewport == null) mViewport = mScrollRect.viewport != null ? mScrollRect.viewport : (RectTransform)mScrollRect.transform;
                if (mContent == null) mContent = mScrollRect.content;
            }
        }

        /// <summary>
        /// 设置数据源并刷新虚拟视口
        /// </summary>
        public void SetData<T>(IList<T> list)
        {
            EnsureComponents();
            mDataList = (IList)list;
            mTotalCount = list != null ? list.Count : 0;

            if (mSelectedIndex >= mTotalCount)
            {
                mSelectedIndex = -1;
            }

            CalculateContentSize();
            Refresh(forceRebind: true);
        }

        /// <summary>
        /// 计算 Content 的总尺寸
        /// </summary>
        private void CalculateContentSize()
        {
            if (mContent == null) return;

            float width = 0f;
            float height = 0f;

            switch (mLayout)
            {
                case ListLayout.Vertical:
                    height = mPadding.top + mPadding.bottom + mTotalCount * mItemSize.y + Mathf.Max(0, mTotalCount - 1) * mSpacing.y;
                    width = mContent.sizeDelta.x;
                    break;

                case ListLayout.Horizontal:
                    width = mPadding.left + mPadding.right + mTotalCount * mItemSize.x + Mathf.Max(0, mTotalCount - 1) * mSpacing.x;
                    height = mContent.sizeDelta.y;
                    break;

                case ListLayout.VerticalGrid:
                    int cols = Mathf.Max(1, mConstraintCount);
                    int rows = Mathf.CeilToInt((float)mTotalCount / cols);
                    height = mPadding.top + mPadding.bottom + rows * mItemSize.y + Mathf.Max(0, rows - 1) * mSpacing.y;
                    width = mContent.sizeDelta.x;
                    break;

                case ListLayout.HorizontalGrid:
                    int gridRows = Mathf.Max(1, mConstraintCount);
                    int gridCols = Mathf.CeilToInt((float)mTotalCount / gridRows);
                    width = mPadding.left + mPadding.right + gridCols * mItemSize.x + Mathf.Max(0, gridCols - 1) * mSpacing.x;
                    height = mContent.sizeDelta.y;
                    break;
            }

            mContent.sizeDelta = new Vector2(width, height);
        }

        private void OnScroll(Vector2 scrollPos)
        {
            Refresh(forceRebind: false);
        }

        /// <summary>
        /// 刷新视口可见元素
        /// </summary>
        public void Refresh(bool forceRebind = false)
        {
            if (mTotalCount == 0 || mViewport == null || mContent == null)
            {
                RecycleAll();
                return;
            }

            GetVisibleRange(out int startIndex, out int endIndex);

            // 1. 回收滑出视口的项
            var outOfRange = new List<int>();
            foreach (var kv in mVisibleItems)
            {
                int index = kv.Key;
                if (index < startIndex || index > endIndex)
                {
                    outOfRange.Add(index);
                }
            }

            for (int i = 0; i < outOfRange.Count; i++)
            {
                int index = outOfRange[i];
                var item = mVisibleItems[index];
                mVisibleItems.Remove(index);
                RecycleItem(item);
            }

            // 2. 补充或更新视口范围内的项
            for (int index = startIndex; index <= endIndex; index++)
            {
                if (mVisibleItems.TryGetValue(index, out var item))
                {
                    if (forceRebind)
                    {
                        item.BindData(mDataList[index], index);
                        ItemBound?.Invoke(item);
                        item.SetSelected(index == mSelectedIndex, isUserAction: false);
                    }
                }
                else
                {
                    item = GetOrCreateItem();
                    item.gameObject.SetActive(true);
                    PositionItem(item, index);
                    item.Index = index;
                    item.BindData(mDataList[index], index);
                    ItemBound?.Invoke(item);
                    item.SetSelected(index == mSelectedIndex, isUserAction: false);
                    mVisibleItems[index] = item;
                }
            }
        }

        /// <summary>
        /// 计算当前视口覆盖的数据索引范围
        /// </summary>
        private void GetVisibleRange(out int startIndex, out int endIndex)
        {
            startIndex = 0;
            endIndex = mTotalCount - 1;

            if (mContent == null || mViewport == null) return;

            Vector2 contentPos = mContent.anchoredPosition;

            switch (mLayout)
            {
                case ListLayout.Vertical:
                    float topY = Mathf.Max(0f, contentPos.y - mPadding.top);
                    float viewH = mViewport.rect.height;
                    float stepY = mItemSize.y + mSpacing.y;

                    startIndex = Mathf.FloorToInt(topY / stepY) - mBufferCount;
                    endIndex = Mathf.CeilToInt((topY + viewH) / stepY) + mBufferCount;
                    break;

                case ListLayout.Horizontal:
                    float leftX = Mathf.Max(0f, -contentPos.x - mPadding.left);
                    float viewW = mViewport.rect.width;
                    float stepX = mItemSize.x + mSpacing.x;

                    startIndex = Mathf.FloorToInt(leftX / stepX) - mBufferCount;
                    endIndex = Mathf.CeilToInt((leftX + viewW) / stepX) + mBufferCount;
                    break;

                case ListLayout.VerticalGrid:
                    int cols = Mathf.Max(1, mConstraintCount);
                    float gTopY = Mathf.Max(0f, contentPos.y - mPadding.top);
                    float gViewH = mViewport.rect.height;
                    float gStepY = mItemSize.y + mSpacing.y;

                    int startRow = Mathf.FloorToInt(gTopY / gStepY) - mBufferCount;
                    int endRow = Mathf.CeilToInt((gTopY + gViewH) / gStepY) + mBufferCount;

                    startIndex = startRow * cols;
                    endIndex = (endRow + 1) * cols - 1;
                    break;

                case ListLayout.HorizontalGrid:
                    int rows = Mathf.Max(1, mConstraintCount);
                    float gLeftX = Mathf.Max(0f, -contentPos.x - mPadding.left);
                    float gViewW = mViewport.rect.width;
                    float gStepX = mItemSize.x + mSpacing.x;

                    int startCol = Mathf.FloorToInt(gLeftX / gStepX) - mBufferCount;
                    int endCol = Mathf.CeilToInt((gLeftX + gViewW) / gStepX) + mBufferCount;

                    startIndex = startCol * rows;
                    endIndex = (endCol + 1) * rows - 1;
                    break;
            }

            startIndex = Mathf.Clamp(startIndex, 0, mTotalCount - 1);
            endIndex = Mathf.Clamp(endIndex, 0, mTotalCount - 1);
        }

        /// <summary>
        /// 布局定位单个列表项
        /// </summary>
        private void PositionItem(UIListItem item, int index)
        {
            var rect = item.GetComponent<RectTransform>();
            if (rect == null) return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = mItemSize;

            float posX = 0f;
            float posY = 0f;

            switch (mLayout)
            {
                case ListLayout.Vertical:
                    posX = mPadding.left;
                    posY = -(mPadding.top + index * (mItemSize.y + mSpacing.y));
                    break;

                case ListLayout.Horizontal:
                    posX = mPadding.left + index * (mItemSize.x + mSpacing.x);
                    posY = -mPadding.top;
                    break;

                case ListLayout.VerticalGrid:
                    int cols = Mathf.Max(1, mConstraintCount);
                    int col = index % cols;
                    int row = index / cols;
                    posX = mPadding.left + col * (mItemSize.x + mSpacing.x);
                    posY = -(mPadding.top + row * (mItemSize.y + mSpacing.y));
                    break;

                case ListLayout.HorizontalGrid:
                    int gRows = Mathf.Max(1, mConstraintCount);
                    int gRow = index % gRows;
                    int gCol = index / gRows;
                    posX = mPadding.left + gCol * (mItemSize.x + mSpacing.x);
                    posY = -(mPadding.top + gRow * (mItemSize.y + mSpacing.y));
                    break;
            }

            rect.anchoredPosition = new Vector2(posX, posY);
        }

        public bool SelectByIndex(int index, bool isUserAction = false, bool notify = true)
        {
            if (!mEnableSelection) return false;

            if (index < 0 || index >= mTotalCount)
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

            // 刷新可见项的状态
            foreach (var kv in mVisibleItems)
            {
                kv.Value.SetSelected(kv.Key == mSelectedIndex, isUserAction);
            }

            if (notify)
            {
                SelectionChanged?.Invoke(mSelectedIndex, oldIndex, isUserAction);
            }

            return true;
        }

        public void ClearSelection(bool isUserAction = false, bool notify = true)
        {
            if (mSelectedIndex == -1) return;

            int oldIndex = mSelectedIndex;
            mSelectedIndex = -1;

            foreach (var kv in mVisibleItems)
            {
                kv.Value.SetSelected(false, isUserAction);
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
                    var fallbackGo = new GameObject("VirtualItem");
                    fallbackGo.transform.SetParent(mContent, false);
                    item = fallbackGo.AddComponent<UIListItem>();
                }
                else
                {
                    var go = Instantiate(mItemTemplate.gameObject, mContent);
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

        private void RecycleAll()
        {
            foreach (var kv in mVisibleItems)
            {
                RecycleItem(kv.Value);
            }
            mVisibleItems.Clear();
        }

        public void Clear()
        {
            RecycleAll();
            mSelectedIndex = -1;
            mTotalCount = 0;
            mDataList = null;

            if (mContent != null)
            {
                mContent.sizeDelta = Vector2.zero;
            }
        }

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
