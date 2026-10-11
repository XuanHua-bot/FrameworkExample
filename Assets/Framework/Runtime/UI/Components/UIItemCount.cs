using System;
using TMPro;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 道具/货币堆叠数量显示组件（P1 表现组件）
    /// 统一处理大于1显示、大于特定上限省略（如 999+ / 10k）、零值隐藏等格式化逻辑
    /// </summary>
    [DisallowMultipleComponent]
    public class UIItemCount : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_Text mCountText;

        [Header("Formatting Rules")]
        [Tooltip("数量小于等于此数值时自动隐藏（例如只有1个物品时不显示数字1，默认阈值为 1）")]
        [SerializeField] private long mHideIfLessThanOrEqualTo = 1;

        [Tooltip("超过此数量时启用缩写格式，例如 9999 显示为 999+ 或 10k。0 表示不限制")]
        [SerializeField] private long mMaxDisplayLimit = 999;

        [Tooltip("达到上限后的显示后缀格式，如 '+' 或 'k'")]
        [SerializeField] private string mLimitSuffix = "+";

        private long mCurrentCount;

        public long Count => mCurrentCount;

        private void Awake()
        {
            if (mCountText == null)
            {
                mCountText = GetComponent<TMP_Text>();
            }
        }

        /// <summary>
        /// 设置数量并自动格式化
        /// </summary>
        public void SetCount(long count)
        {
            mCurrentCount = count;

            if (count <= mHideIfLessThanOrEqualTo)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (mCountText != null)
            {
                if (mMaxDisplayLimit > 0 && count > mMaxDisplayLimit)
                {
                    mCountText.text = $"{mMaxDisplayLimit}{mLimitSuffix}";
                }
                else
                {
                    mCountText.text = count.ToString();
                }
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            mCurrentCount = 0;
            if (mCountText != null)
            {
                mCountText.text = string.Empty;
            }
            gameObject.SetActive(false);
        }
    }
}
