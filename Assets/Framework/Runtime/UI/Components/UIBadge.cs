using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 红点 / 徽章角标组件（P1 表现组件）
    /// 支持纯圆点红点、带数字徽标（如未读消息数、待领取奖励）以及 99+ 截断
    /// </summary>
    [DisallowMultipleComponent]
    public class UIBadge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image mBadgeBackground;
        [SerializeField] private TMP_Text mBadgeText;

        [Header("Configuration")]
        [Tooltip("最大数字限制，超过此值显示上限并附加后缀，如 99+")]
        [SerializeField] private int mMaxNumber = 99;

        private int mCurrentCount;

        public int Count => mCurrentCount;
        public bool IsVisible => gameObject.activeSelf;

        private void Awake()
        {
            if (mBadgeBackground == null)
            {
                mBadgeBackground = GetComponent<Image>();
            }
        }

        /// <summary>
        /// 显示纯红点（无数字）
        /// </summary>
        public void ShowDot()
        {
            mCurrentCount = 1;
            if (mBadgeText != null)
            {
                mBadgeText.gameObject.SetActive(false);
            }
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 显示带数字的角标
        /// </summary>
        /// <param name="count">大于0显示，小于等于0自动隐藏</param>
        public void SetCount(int count)
        {
            mCurrentCount = count;

            if (count <= 0)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);

            if (mBadgeText != null)
            {
                mBadgeText.gameObject.SetActive(true);
                if (mMaxNumber > 0 && count > mMaxNumber)
                {
                    mBadgeText.text = $"{mMaxNumber}+";
                }
                else
                {
                    mBadgeText.text = count.ToString();
                }
            }
        }

        public void Hide()
        {
            mCurrentCount = 0;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            Hide();
            if (mBadgeText != null)
            {
                mBadgeText.text = string.Empty;
                mBadgeText.gameObject.SetActive(false);
            }
        }
    }
}
