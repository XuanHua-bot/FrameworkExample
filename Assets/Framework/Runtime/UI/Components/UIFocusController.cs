using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// UI 焦点与键盘导航控制器（P3 表现组件）
    /// 支持手柄/键盘 Tab 键在各可交互元素间切换焦点
    /// 支持设定默认焦点元素、循环导航规则以及焦点变更事件
    /// </summary>
    [DisallowMultipleComponent]
    public class UIFocusController : MonoBehaviour
    {
        [Header("Focus Elements")]
        [Tooltip("受管理的 Selectable 元素列表（可自动搜寻或手动指定顺序）")]
        [SerializeField] private List<Selectable> mElements = new List<Selectable>();

        [Tooltip("进入界面时默认选中的元素")]
        [SerializeField] private Selectable mDefaultElement;

        [Header("Rules")]
        [Tooltip("是否允许首尾循环切换焦点")]
        [SerializeField] private bool mWrapAround = true;

        [Tooltip("是否在打开时自动为默认元素赋予焦点")]
        [SerializeField] private bool mAutoFocusOnEnable = true;

        private Selectable mCurrentFocused;

        public Selectable CurrentFocused => mCurrentFocused;

        /// <summary>
        /// 焦点改变事件 (newFocused, oldFocused)
        /// </summary>
        public event Action<Selectable, Selectable> FocusChanged;

        private void OnEnable()
        {
            if (mAutoFocusOnEnable)
            {
                FocusDefault();
            }
        }

        /// <summary>
        /// 自动搜寻子节点下的所有 Selectable
        /// </summary>
        public void AutoCollectElements()
        {
            mElements.Clear();
            GetComponentsInChildren(includeInactive: false, mElements);
        }

        /// <summary>
        /// 将焦点置于默认元素
        /// </summary>
        public void FocusDefault()
        {
            if (mDefaultElement != null && mDefaultElement.interactable && mDefaultElement.gameObject.activeInHierarchy)
            {
                SetFocus(mDefaultElement);
            }
            else if (mElements.Count > 0 && mElements[0] != null)
            {
                SetFocus(mElements[0]);
            }
        }

        /// <summary>
        /// 聚焦下一个元素
        /// </summary>
        public void FocusNext()
        {
            MoveFocus(1);
        }

        /// <summary>
        /// 聚焦上一个元素
        /// </summary>
        public void FocusPrevious()
        {
            MoveFocus(-1);
        }

        private void MoveFocus(int step)
        {
            if (mElements == null || mElements.Count == 0) return;

            int currentIndex = mElements.IndexOf(mCurrentFocused);
            int count = mElements.Count;

            int nextIndex = currentIndex + step;
            if (mWrapAround)
            {
                nextIndex = (nextIndex % count + count) % count;
            }
            else
            {
                nextIndex = Mathf.Clamp(nextIndex, 0, count - 1);
            }

            SetFocus(mElements[nextIndex]);
        }

        /// <summary>
        /// 设置当前焦点
        /// </summary>
        public void SetFocus(Selectable target)
        {
            if (target == null || !target.interactable || !target.gameObject.activeInHierarchy)
            {
                return;
            }

            var oldFocused = mCurrentFocused;
            mCurrentFocused = target;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(target.gameObject);
            }

            FocusChanged?.Invoke(mCurrentFocused, oldFocused);
        }

        public virtual void ResetForPool()
        {
            mCurrentFocused = null;
            FocusChanged = null;
        }

        private void OnDestroy()
        {
            FocusChanged = null;
            mCurrentFocused = null;
        }
    }
}
