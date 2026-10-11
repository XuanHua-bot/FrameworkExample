using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 可选控件复合表现状态（Normal, Selected, Disabled, Highlighted, Busy, Locked）
    /// </summary>
    public enum UIInteractiveVisualState
    {
        Normal,
        Selected,
        Highlighted,
        Disabled,
        Busy,
        Locked
    }

    /// <summary>
    /// 统一状态表现控制器组件（P3 表现组件）
    /// 统筹管理按钮、列表项、Tab 等控件在 Normal、Selected、Highlighted、Disabled、Busy、Locked 各状态下的外观
    /// 支持 GameObject 显隐、颜色渐变、Sprite 切换，杜绝多套状态表现脚本各自为政
    /// </summary>
    [DisallowMultipleComponent]
    public class UISelectableState : MonoBehaviour
    {
        [Header("State GameObjects (Optional)")]
        [SerializeField] private GameObject mNormalObject;
        [SerializeField] private GameObject mSelectedObject;
        [SerializeField] private GameObject mHighlightedObject;
        [SerializeField] private GameObject mDisabledObject;
        [SerializeField] private GameObject mBusyObject;
        [SerializeField] private GameObject mLockedObject;

        [Header("Graphic & Colors (Optional)")]
        [SerializeField] private Graphic mTargetGraphic;
        [SerializeField] private Color mNormalColor = Color.white;
        [SerializeField] private Color mSelectedColor = new Color(1f, 0.9f, 0.5f, 1f);
        [SerializeField] private Color mHighlightedColor = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField] private Color mDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        [SerializeField] private Color mBusyColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private Color mLockedColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        [Header("Sprite Swap (Optional)")]
        [SerializeField] private Image mTargetImage;
        [SerializeField] private Sprite mNormalSprite;
        [SerializeField] private Sprite mSelectedSprite;
        [SerializeField] private Sprite mDisabledSprite;

        private UIInteractiveVisualState mCurrentState = UIInteractiveVisualState.Normal;

        public UIInteractiveVisualState CurrentState => mCurrentState;

        /// <summary>
        /// 状态切换事件
        /// </summary>
        public event Action<UIInteractiveVisualState> StateChanged;

        private void Awake()
        {
            if (mTargetGraphic == null)
            {
                mTargetGraphic = GetComponent<Graphic>();
            }

            if (mTargetImage == null)
            {
                mTargetImage = GetComponent<Image>();
            }
        }

        private void Start()
        {
            ApplyState(mCurrentState);
        }

        /// <summary>
        /// 切换到指定交互表现状态
        /// </summary>
        public void SetState(UIInteractiveVisualState state)
        {
            if (mCurrentState == state) return;

            mCurrentState = state;
            ApplyState(state);
            StateChanged?.Invoke(state);
        }

        public void SetSelected(bool selected)
        {
            SetState(selected ? UIInteractiveVisualState.Selected : UIInteractiveVisualState.Normal);
        }

        public void SetDisabled(bool disabled)
        {
            SetState(disabled ? UIInteractiveVisualState.Disabled : UIInteractiveVisualState.Normal);
        }

        public void SetLocked(bool locked)
        {
            SetState(locked ? UIInteractiveVisualState.Locked : UIInteractiveVisualState.Normal);
        }

        private void ApplyState(UIInteractiveVisualState state)
        {
            // 1. GameObject 显隐切换
            if (mNormalObject != null) mNormalObject.SetActive(state == UIInteractiveVisualState.Normal);
            if (mSelectedObject != null) mSelectedObject.SetActive(state == UIInteractiveVisualState.Selected);
            if (mHighlightedObject != null) mHighlightedObject.SetActive(state == UIInteractiveVisualState.Highlighted);
            if (mDisabledObject != null) mDisabledObject.SetActive(state == UIInteractiveVisualState.Disabled);
            if (mBusyObject != null) mBusyObject.SetActive(state == UIInteractiveVisualState.Busy);
            if (mLockedObject != null) mLockedObject.SetActive(state == UIInteractiveVisualState.Locked);

            // 2. 颜色染色
            if (mTargetGraphic != null)
            {
                switch (state)
                {
                    case UIInteractiveVisualState.Normal: mTargetGraphic.color = mNormalColor; break;
                    case UIInteractiveVisualState.Selected: mTargetGraphic.color = mSelectedColor; break;
                    case UIInteractiveVisualState.Highlighted: mTargetGraphic.color = mHighlightedColor; break;
                    case UIInteractiveVisualState.Disabled: mTargetGraphic.color = mDisabledColor; break;
                    case UIInteractiveVisualState.Busy: mTargetGraphic.color = mBusyColor; break;
                    case UIInteractiveVisualState.Locked: mTargetGraphic.color = mLockedColor; break;
                }
            }

            // 3. Sprite 切换
            if (mTargetImage != null)
            {
                switch (state)
                {
                    case UIInteractiveVisualState.Normal:
                        if (mNormalSprite != null) mTargetImage.sprite = mNormalSprite;
                        break;
                    case UIInteractiveVisualState.Selected:
                        if (mSelectedSprite != null) mTargetImage.sprite = mSelectedSprite;
                        break;
                    case UIInteractiveVisualState.Disabled:
                        if (mDisabledSprite != null) mTargetImage.sprite = mDisabledSprite;
                        break;
                }
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            StateChanged = null;
            mCurrentState = UIInteractiveVisualState.Normal;
            ApplyState(UIInteractiveVisualState.Normal);
        }

        private void OnDestroy()
        {
            StateChanged = null;
        }
    }
}
