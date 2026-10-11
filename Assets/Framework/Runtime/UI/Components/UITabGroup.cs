using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// Tab 选项卡组管理组件
    /// 统一协调多个 UITabItem 的单选互斥逻辑、选中事件通知与关联内容页 (Content Panels) 切换
    /// 满足组件库规范：纯表现与通用交互，支持对象池复用，支持解耦音效与业务 Controller
    /// </summary>
    [DisallowMultipleComponent]
    public class UITabGroup : MonoBehaviour
    {
        [Header("Tab Items")]
        [Tooltip("受管理的 Tab 项列表。若为空可在 Awake 时自动搜寻子物体或通过代码动态注册")]
        [SerializeField] private List<UITabItem> mItems = new List<UITabItem>();

        [Tooltip("是否在初始化时自动搜寻并绑定子物体下的所有 UITabItem")]
        [SerializeField] private bool mAutoCollectChildren = true;

        [Header("Selection Rules")]
        [Tooltip("默认选中的索引（-1 表示默认不选中任何项）")]
        [SerializeField] private int mDefaultSelectedIndex = 0;

        [Tooltip("是否允许取消选中（全部不选中）。常规页签切换通常不允许，多选/开关式标签组可配置为允许")]
        [SerializeField] private bool mAllowSwitchOff = false;

        [Header("Content Panels (Optional)")]
        [Tooltip("与 TabItem 索引一一对应的页签内容节点。切换 Tab 时会自动联动切换对应 Content 的显示/隐藏")]
        [SerializeField] private List<GameObject> mContentPanels = new List<GameObject>();

        [Header("Audio")]
        [Tooltip("组级统一点击音效（若 UITabItem 自身未配置有效音效则退化使用此配置）")]
        [SerializeField] private AudioCueKey mDefaultSelectSound;

        // 运行时状态
        private UITabItem mCurrentSelectedItem;
        private int mCurrentSelectedIndex = -1;
        private bool mIsInitialized;

        /// <summary>
        /// 当前受管理的 Tab 列表（只读）
        /// </summary>
        public IReadOnlyList<UITabItem> Items => mItems;

        /// <summary>
        /// 当前选中的 Tab 项（无选中时为 null）
        /// </summary>
        public UITabItem SelectedItem => mCurrentSelectedItem;

        /// <summary>
        /// 当前选中的索引（无选中时为 -1）
        /// </summary>
        public int SelectedIndex => mCurrentSelectedIndex;

        /// <summary>
        /// 当前选中的 Tab 标识（无选中时为 string.Empty）
        /// </summary>
        public string SelectedKey => mCurrentSelectedItem != null ? mCurrentSelectedItem.TabKey : string.Empty;

        /// <summary>
        /// Tab 选中切换事件通知 (newIndex, oldIndex, isUserAction)
        /// 页面 Controller 应订阅此事件以执行业务查询或触发对应 SubPresenter / Controller 逻辑
        /// </summary>
        public event Action<int, int, bool> TabChanged;

        /// <summary>
        /// Tab 选中切换事件通知 (selectedItem, isUserAction)
        /// </summary>
        public event Action<UITabItem, bool> ItemSelected;

        /// <summary>
        /// 音频播放请求事件（解耦 IAudioService）
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            if (mDefaultSelectedIndex >= 0 && mDefaultSelectedIndex < mItems.Count && mCurrentSelectedItem == null)
            {
                SelectByIndex(mDefaultSelectedIndex, isUserAction: false, notify: true);
            }
        }

        /// <summary>
        /// 确保初始化
        /// </summary>
        public void EnsureInitialized()
        {
            if (mIsInitialized)
            {
                return;
            }

            mIsInitialized = true;

            if (mAutoCollectChildren && (mItems == null || mItems.Count == 0))
            {
                mItems = new List<UITabItem>();
                GetComponentsInChildren(includeInactive: true, mItems);
            }

            RefreshBindings();
        }

        /// <summary>
        /// 刷新内部绑定的索引关系与事件监听
        /// </summary>
        public void RefreshBindings()
        {
            if (mItems == null)
            {
                return;
            }

            for (int i = 0; i < mItems.Count; i++)
            {
                var item = mItems[i];
                if (item == null) continue;

                item.Index = i;
                item.BindGroup(this);
            }
        }

        /// <summary>
        /// 动态注册新的 Tab 项
        /// </summary>
        public void RegisterItem(UITabItem item)
        {
            if (item == null || mItems.Contains(item))
            {
                return;
            }

            item.Index = mItems.Count;
            item.BindGroup(this);
            mItems.Add(item);
        }

        /// <summary>
        /// 动态注销 Tab 项
        /// </summary>
        public void UnregisterItem(UITabItem item)
        {
            if (item == null || !mItems.Contains(item))
            {
                return;
            }

            if (mCurrentSelectedItem == item)
            {
                mCurrentSelectedItem = null;
                mCurrentSelectedIndex = -1;
            }

            item.UnbindGroup();
            mItems.Remove(item);
            RefreshBindings();
        }

        /// <summary>
        /// 清空所有注册项
        /// </summary>
        public void ClearItems()
        {
            if (mItems != null)
            {
                for (int i = 0; i < mItems.Count; i++)
                {
                    if (mItems[i] != null)
                    {
                        mItems[i].UnbindGroup();
                    }
                }
                mItems.Clear();
            }

            mCurrentSelectedItem = null;
            mCurrentSelectedIndex = -1;
        }

        /// <summary>
        /// 设置对应索引的关联面板（Content Panel）
        /// </summary>
        public void SetContentPanels(List<GameObject> panels)
        {
            mContentPanels = panels;
            UpdateContentPanelsVisibility();
        }

        /// <summary>
        /// 选中指定索引的 Tab
        /// </summary>
        /// <param name="index">目标索引</param>
        /// <param name="isUserAction">是否是用户交互触发</param>
        /// <param name="notify">是否分发事件通知</param>
        /// <returns>是否成功切换</returns>
        public bool SelectByIndex(int index, bool isUserAction = false, bool notify = true)
        {
            if (index < 0 || index >= mItems.Count)
            {
                if (mAllowSwitchOff && index == -1)
                {
                    DeselectAll(isUserAction, notify);
                    return true;
                }
                return false;
            }

            return SelectItemInternal(mItems[index], isUserAction, notify);
        }

        /// <summary>
        /// 根据 TabKey 选中
        /// </summary>
        public bool SelectByKey(string tabKey, bool isUserAction = false, bool notify = true)
        {
            if (string.IsNullOrEmpty(tabKey))
            {
                return false;
            }

            for (int i = 0; i < mItems.Count; i++)
            {
                var item = mItems[i];
                if (item != null && item.TabKey == tabKey)
                {
                    return SelectItemInternal(item, isUserAction, notify);
                }
            }

            return false;
        }

        /// <summary>
        /// 取消全部选中（当 AllowSwitchOff 为 true 时生效）
        /// </summary>
        public void DeselectAll(bool isUserAction = false, bool notify = true)
        {
            if (!mAllowSwitchOff)
            {
                return;
            }

            int oldIndex = mCurrentSelectedIndex;
            mCurrentSelectedIndex = -1;
            mCurrentSelectedItem = null;

            for (int i = 0; i < mItems.Count; i++)
            {
                var item = mItems[i];
                if (item != null)
                {
                    item.SetSelected(false, isUserAction, notifyGroup: false);
                }
            }

            UpdateContentPanelsVisibility();

            if (notify && oldIndex != -1)
            {
                TabChanged?.Invoke(-1, oldIndex, isUserAction);
                ItemSelected?.Invoke(null, isUserAction);
            }
        }

        /// <summary>
        /// 由 UITabItem 被点击或状态请求时调用
        /// </summary>
        internal void OnItemSelectRequested(UITabItem item, bool isUserAction)
        {
            if (item == null) return;

            // 如果点击的是已经选中的项
            if (mCurrentSelectedItem == item)
            {
                if (mAllowSwitchOff)
                {
                    DeselectAll(isUserAction, notify: true);
                }
                return;
            }

            SelectItemInternal(item, isUserAction, notify: true);
        }

        private bool SelectItemInternal(UITabItem targetItem, bool isUserAction, bool notify)
        {
            if (targetItem == null || !targetItem.Interactable)
            {
                return false;
            }

            if (mCurrentSelectedItem == targetItem)
            {
                return false;
            }

            int oldIndex = mCurrentSelectedIndex;
            var oldItem = mCurrentSelectedItem;

            mCurrentSelectedItem = targetItem;
            mCurrentSelectedIndex = targetItem.Index;

            // 互斥控制：遍历关闭其他项，激活目标项
            for (int i = 0; i < mItems.Count; i++)
            {
                var item = mItems[i];
                if (item == null) continue;

                bool shouldSelect = (item == targetItem);
                item.SetSelected(shouldSelect, isUserAction, notifyGroup: false);
            }

            // 联动关联内容面板
            UpdateContentPanelsVisibility();

            // 触发音效请求（若组有统一配置且 item 没有有效声音则上报组声音）
            if (isUserAction && !targetItem.HasSelectSound && mDefaultSelectSound.IsValid)
            {
                SoundRequested?.Invoke(mDefaultSelectSound);
            }

            if (notify)
            {
                TabChanged?.Invoke(mCurrentSelectedIndex, oldIndex, isUserAction);
                ItemSelected?.Invoke(mCurrentSelectedItem, isUserAction);
            }

            return true;
        }

        private void UpdateContentPanelsVisibility()
        {
            if (mContentPanels == null || mContentPanels.Count == 0)
            {
                return;
            }

            for (int i = 0; i < mContentPanels.Count; i++)
            {
                var panel = mContentPanels[i];
                if (panel != null)
                {
                    bool isActive = (i == mCurrentSelectedIndex);
                    panel.SetActive(isActive);
                }
            }
        }

        /// <summary>
        /// 重置状态（供对象池回收或页面注销复用）
        /// </summary>
        public virtual void ResetForPool()
        {
            TabChanged = null;
            ItemSelected = null;
            SoundRequested = null;

            mCurrentSelectedItem = null;
            mCurrentSelectedIndex = -1;

            if (mItems != null)
            {
                for (int i = 0; i < mItems.Count; i++)
                {
                    var item = mItems[i];
                    if (item != null)
                    {
                        item.ResetForPool();
                    }
                }
            }

            UpdateContentPanelsVisibility();
        }

        private void OnDestroy()
        {
            TabChanged = null;
            ItemSelected = null;
            SoundRequested = null;
            mCurrentSelectedItem = null;
        }
    }
}
