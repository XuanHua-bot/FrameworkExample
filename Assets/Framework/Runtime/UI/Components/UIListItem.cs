using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameFramework
{
    /// <summary>
    /// 统一列表项基础组件（P1 表现组件）
    /// 支持在 UISimpleList 或 UIVirtualList 中被驱动和复用
    /// 统一封装选中、点击、长按/双击、高亮、数据绑定与对象池重置
    /// </summary>
    [DisallowMultipleComponent]
    public class UIListItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Selection Visuals (Optional)")]
        [Tooltip("选中状态高亮物体")]
        [SerializeField] private GameObject mSelectedIndicator;

        [Tooltip("鼠标悬停/高亮物体")]
        [SerializeField] private GameObject mHoverIndicator;

        [Header("Audio")]
        [SerializeField] private AudioCueKey mClickSound;

        // 运行时状态
        private int mIndex = -1;
        private object mData;
        private bool mIsSelected;
        private bool mInteractable = true;

        public int Index
        {
            get => mIndex;
            internal set => mIndex = value;
        }

        public object RawData => mData;
        public bool IsSelected => mIsSelected;

        public bool Interactable
        {
            get => mInteractable;
            set => mInteractable = value;
        }

        /// <summary>
        /// 点击事件 (item, isUserAction)
        /// </summary>
        public event Action<UIListItem, bool> Clicked;

        /// <summary>
        /// 选中状态变更事件 (isSelected, isUserAction)
        /// </summary>
        public event Action<bool, bool> SelectedChanged;

        /// <summary>
        /// 音效请求事件（解耦 IAudioService）
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        /// <summary>
        /// 绑定数据。派生类可重写以将具体业务数据赋给各个 UI 控件
        /// </summary>
        public virtual void BindData(object data, int index)
        {
            mData = data;
            mIndex = index;
            OnDataBound();
        }

        /// <summary>
        /// 数据绑定完成虚方法，派生组件实现视觉刷新
        /// </summary>
        protected virtual void OnDataBound()
        {
        }

        /// <summary>
        /// 设置选中状态
        /// </summary>
        public virtual void SetSelected(bool selected, bool isUserAction = false)
        {
            if (mIsSelected == selected) return;

            mIsSelected = selected;

            if (mSelectedIndicator != null)
            {
                mSelectedIndicator.SetActive(selected);
            }

            SelectedChanged?.Invoke(selected, isUserAction);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!mInteractable || !isActiveAndEnabled) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (mClickSound.IsValid)
            {
                SoundRequested?.Invoke(mClickSound);
            }

            Clicked?.Invoke(this, true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (mHoverIndicator != null && mInteractable)
            {
                mHoverIndicator.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (mHoverIndicator != null)
            {
                mHoverIndicator.SetActive(false);
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            GetComponent<UIDragHandle>()?.ResetForPool();
            mIndex = -1;
            mData = null;
            mIsSelected = false;
            mInteractable = true;

            Clicked = null;
            SelectedChanged = null;
            SoundRequested = null;

            if (mSelectedIndicator != null)
            {
                mSelectedIndicator.SetActive(false);
            }

            if (mHoverIndicator != null)
            {
                mHoverIndicator.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Clicked = null;
            SelectedChanged = null;
            SoundRequested = null;
        }
    }
}
