using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 统一进度条组件（P1 表现组件）
    /// 支持 Slider / Image.fillAmount 两种表现形式
    /// 支持平滑数值缓动、文本百分比/绝对值格式化，适配 Concave 的 Progress Bar 预制体
    /// </summary>
    [DisallowMultipleComponent]
    public class UIProgressBar : MonoBehaviour
    {
        [Header("Targets (Either Image or Slider)")]
        [Tooltip("填充图片（Image Type 需为 Filled）")]
        [SerializeField] private Image mFillImage;

        [Tooltip("原生 Slider（可选，与 FillImage 二选一）")]
        [SerializeField] private Slider mSlider;

        [Header("Texts (Optional)")]
        [Tooltip("进度文本控件")]
        [SerializeField] private TMP_Text mAmountText;

        [Tooltip("文本格式：{0} 当前值，{1} 最大值，{2} 百分比(0-100)。如 '{0}/{1}' 或 '{2:0}%'")]
        [SerializeField] private string mFormat = "{0}/{1}";

        [Header("Animation")]
        [Tooltip("是否启用平滑插值缓动")]
        [SerializeField] private bool mSmoothTransition = true;

        [Tooltip("缓动时长（秒）")]
        [SerializeField] private float mTransitionDuration = 0.25f;

        // 运行时状态
        private float mCurrentValue;
        private float mMaxValue = 100f;
        private float mTargetProgress; // 0 ~ 1
        private float mDisplayedProgress; // 0 ~ 1
        private Coroutine mTransitionCoroutine;

        public float CurrentValue => mCurrentValue;
        public float MaxValue => mMaxValue;
        public float Progress => mDisplayedProgress;

        /// <summary>
        /// 进度变化事件 (currentProgress, currentValue, maxValue)
        /// </summary>
        public event Action<float, float, float> ProgressChanged;

        private void Awake()
        {
            if (mFillImage == null && mSlider == null)
            {
                mFillImage = GetComponent<Image>();
                if (mFillImage == null)
                {
                    mSlider = GetComponent<Slider>();
                }
            }
        }

        /// <summary>
        /// 设置当前值和最大值
        /// </summary>
        /// <param name="current">当前数值</param>
        /// <param name="max">最大数值（必须大于0）</param>
        /// <param name="instant">是否跳过缓动立即更新</param>
        public void SetProgress(float current, float max, bool instant = false)
        {
            mCurrentValue = Mathf.Max(0f, current);
            mMaxValue = Mathf.Max(0.0001f, max);
            float target = Mathf.Clamp01(mCurrentValue / mMaxValue);

            mTargetProgress = target;

            if (instant || !mSmoothTransition || !isActiveAndEnabled)
            {
                if (mTransitionCoroutine != null)
                {
                    StopCoroutine(mTransitionCoroutine);
                    mTransitionCoroutine = null;
                }
                ApplyProgress(target);
            }
            else
            {
                if (mTransitionCoroutine != null)
                {
                    StopCoroutine(mTransitionCoroutine);
                }
                mTransitionCoroutine = StartCoroutine(TransitionRoutine(target));
            }
        }

        /// <summary>
        /// 直接设置 0~1 的归一化进度
        /// </summary>
        public void SetNormalizedProgress(float progress, bool instant = false)
        {
            SetProgress(progress, 1f, instant);
        }

        private IEnumerator TransitionRoutine(float target)
        {
            float start = mDisplayedProgress;
            float elapsed = 0f;

            while (elapsed < mTransitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / mTransitionDuration);
                float currentP = Mathf.Lerp(start, target, t);
                ApplyProgress(currentP);
                yield return null;
            }

            ApplyProgress(target);
            mTransitionCoroutine = null;
        }

        private void ApplyProgress(float p)
        {
            mDisplayedProgress = p;

            if (mFillImage != null)
            {
                mFillImage.fillAmount = p;
            }

            if (mSlider != null)
            {
                mSlider.value = p;
            }

            UpdateText();
            ProgressChanged?.Invoke(p, mCurrentValue, mMaxValue);
        }

        private void UpdateText()
        {
            if (mAmountText == null) return;

            float current = mDisplayedProgress * mMaxValue;
            float percent = mDisplayedProgress * 100f;

            try
            {
                mAmountText.text = string.Format(mFormat, Mathf.RoundToInt(current), Mathf.RoundToInt(mMaxValue), percent);
            }
            catch
            {
                mAmountText.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(mMaxValue)}";
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            if (mTransitionCoroutine != null)
            {
                StopCoroutine(mTransitionCoroutine);
                mTransitionCoroutine = null;
            }

            mCurrentValue = 0f;
            mMaxValue = 100f;
            mTargetProgress = 0f;
            mDisplayedProgress = 0f;
            ProgressChanged = null;

            ApplyProgress(0f);
        }

        private void OnDestroy()
        {
            if (mTransitionCoroutine != null)
            {
                StopCoroutine(mTransitionCoroutine);
                mTransitionCoroutine = null;
            }
            ProgressChanged = null;
        }
    }
}
