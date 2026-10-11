using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 统一技能/道具冷却遮罩组件（P1 表现组件）
    /// 支持 Radial 扇形转圈冷却遮罩或垂直/水平滑动遮罩
    /// 支持倒计时秒数格式化、CD 结束闪光/回调，与 UIButton.BeginCooldown 完美配合
    /// </summary>
    [DisallowMultipleComponent]
    public class UICooldownView : MonoBehaviour
    {
        [Header("Mask References")]
        [Tooltip("冷却遮罩图片（Image Type 通常为 Filled，例如 Radial 360）")]
        [SerializeField] private Image mCooldownMask;

        [Tooltip("倒计时文本控件（可选）")]
        [SerializeField] private TMP_Text mCountdownText;

        [Header("Rules")]
        [Tooltip("秒数小于此数值时保留一位小数（如 2.4s），大于等于则显示整数秒（如 5s）")]
        [SerializeField] private float mDecimalThreshold = 3f;

        [Tooltip("遮罩填充方向是否反转（默认 true：CD刚开始遮罩满，随着时间推移遮罩减少）")]
        [SerializeField] private bool mInvertFill = true;

        // 运行时状态
        private float mTotalDuration;
        private float mRemainingTime;
        private Coroutine mCooldownCoroutine;

        public bool IsInCooldown => mRemainingTime > 0f;
        public float RemainingTime => mRemainingTime;
        public float TotalDuration => mTotalDuration;

        /// <summary>
        /// 冷却结束回调
        /// </summary>
        public event Action CooldownFinished;

        private void Awake()
        {
            if (mCooldownMask == null)
            {
                mCooldownMask = GetComponent<Image>();
            }

            // 初始时默认未进入冷却
            SetVisualProgress(0f, 0f);
        }

        /// <summary>
        /// 启动冷却倒计时
        /// </summary>
        /// <param name="durationSeconds">冷却总时长</param>
        public void StartCooldown(float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                ClearCooldown();
                return;
            }

            mTotalDuration = durationSeconds;
            mRemainingTime = durationSeconds;

            if (mCooldownCoroutine != null)
            {
                StopCoroutine(mCooldownCoroutine);
            }

            gameObject.SetActive(true);
            mCooldownCoroutine = StartCoroutine(CooldownRoutine());
        }

        /// <summary>
        /// 手动外部驱动（例如由 UIButton 或外部 Timer 每帧同步）
        /// </summary>
        public void UpdateCooldown(float remaining, float total)
        {
            mRemainingTime = Mathf.Max(0f, remaining);
            mTotalDuration = total;

            if (mCooldownCoroutine != null)
            {
                StopCoroutine(mCooldownCoroutine);
                mCooldownCoroutine = null;
            }

            SetVisualProgress(mRemainingTime, mTotalDuration);

            if (mRemainingTime <= 0f)
            {
                ClearCooldown();
            }
        }

        /// <summary>
        /// 清除冷却状态
        /// </summary>
        public void ClearCooldown()
        {
            if (mCooldownCoroutine != null)
            {
                StopCoroutine(mCooldownCoroutine);
                mCooldownCoroutine = null;
            }

            mRemainingTime = 0f;
            mTotalDuration = 0f;

            SetVisualProgress(0f, 0f);
            CooldownFinished?.Invoke();
        }

        private IEnumerator CooldownRoutine()
        {
            while (mRemainingTime > 0f)
            {
                SetVisualProgress(mRemainingTime, mTotalDuration);
                yield return null;
                mRemainingTime -= Time.unscaledDeltaTime;
            }

            mRemainingTime = 0f;
            SetVisualProgress(0f, mTotalDuration);
            mCooldownCoroutine = null;
            CooldownFinished?.Invoke();
        }

        private void SetVisualProgress(float remaining, float total)
        {
            bool active = remaining > 0f;

            if (mCooldownMask != null)
            {
                mCooldownMask.gameObject.SetActive(active);
                if (active && total > 0f)
                {
                    float ratio = Mathf.Clamp01(remaining / total);
                    mCooldownMask.fillAmount = mInvertFill ? ratio : (1f - ratio);
                }
                else
                {
                    mCooldownMask.fillAmount = 0f;
                }
            }

            if (mCountdownText != null)
            {
                mCountdownText.gameObject.SetActive(active);
                if (active)
                {
                    if (remaining < mDecimalThreshold)
                    {
                        mCountdownText.text = remaining.ToString("F1");
                    }
                    else
                    {
                        mCountdownText.text = Mathf.CeilToInt(remaining).ToString();
                    }
                }
                else
                {
                    mCountdownText.text = string.Empty;
                }
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            ClearCooldown();
            CooldownFinished = null;
        }

        private void OnDestroy()
        {
            if (mCooldownCoroutine != null)
            {
                StopCoroutine(mCooldownCoroutine);
                mCooldownCoroutine = null;
            }
            CooldownFinished = null;
        }
    }
}
