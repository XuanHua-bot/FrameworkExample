using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 统一滑动条组件（P2 表现组件）
    /// 封装原生 Slider，提供当前值格式化、步长限制（Step Size）、滑动开始/结束监听及解耦音效
    /// 完美适配 Concave 的 Slider 系列预制体
    /// </summary>
    [DisallowMultipleComponent]
    public class UISlider : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Slider mSlider;

        [Tooltip("数值显示文本（可选）")]
        [SerializeField] private TMP_Text mValueText;

        [Header("Formatting & Step")]
        [Tooltip("文本格式字符串，例如 '{0}'，'{0:0}%' 或 '{0:F1}'")]
        [SerializeField] private string mFormat = "{0}";

        [Tooltip("离散步长（大于0时生效，自动将滑动值对齐到 step 的倍数）")]
        [SerializeField] private float mStepSize = 0f;

        [Header("Audio")]
        [SerializeField] private AudioCueKey mValueChangedSound;

        // 运行时状态
        private float mLastNotifiedValue;

        public float Value
        {
            get => mSlider != null ? mSlider.value : 0f;
            set => SetValue(value);
        }

        public float MinValue
        {
            get => mSlider != null ? mSlider.minValue : 0f;
            set
            {
                if (mSlider != null) mSlider.minValue = value;
            }
        }

        public float MaxValue
        {
            get => mSlider != null ? mSlider.maxValue : 1f;
            set
            {
                if (mSlider != null) mSlider.maxValue = value;
            }
        }

        public bool Interactable
        {
            get => mSlider != null && mSlider.interactable;
            set
            {
                if (mSlider != null) mSlider.interactable = value;
            }
        }

        /// <summary>
        /// 滑动条数值变更事件
        /// </summary>
        public event Action<float> ValueChanged;

        /// <summary>
        /// 音效请求事件（解耦 IAudioService）
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        private void Awake()
        {
            if (mSlider == null)
            {
                mSlider = GetComponent<Slider>();
            }
        }

        private void OnEnable()
        {
            if (mSlider != null)
            {
                mSlider.onValueChanged.AddListener(HandleSliderValueChanged);
            }
            UpdateValueText(Value);
        }

        private void OnDisable()
        {
            if (mSlider != null)
            {
                mSlider.onValueChanged.RemoveListener(HandleSliderValueChanged);
            }
        }

        public void SetValue(float value, bool notify = true)
        {
            if (mSlider == null) return;

            float finalVal = ApplyStep(value);

            if (notify)
            {
                mSlider.value = finalVal;
            }
            else
            {
                mSlider.SetValueWithoutNotify(finalVal);
                UpdateValueText(finalVal);
            }
        }

        private float ApplyStep(float val)
        {
            if (mStepSize > 0f)
            {
                val = Mathf.Round(val / mStepSize) * mStepSize;
            }
            return Mathf.Clamp(val, MinValue, MaxValue);
        }

        private void HandleSliderValueChanged(float rawValue)
        {
            float steppedValue = ApplyStep(rawValue);

            if (mStepSize > 0f && !Mathf.Approximately(steppedValue, rawValue))
            {
                mSlider.SetValueWithoutNotify(steppedValue);
            }

            UpdateValueText(steppedValue);

            if (mValueChangedSound.IsValid && !Mathf.Approximately(steppedValue, mLastNotifiedValue))
            {
                SoundRequested?.Invoke(mValueChangedSound);
            }

            mLastNotifiedValue = steppedValue;
            ValueChanged?.Invoke(steppedValue);
        }

        private void UpdateValueText(float val)
        {
            if (mValueText == null) return;

            try
            {
                mValueText.text = string.Format(mFormat, val);
            }
            catch
            {
                mValueText.text = val.ToString("F1");
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            ValueChanged = null;
            SoundRequested = null;
            if (mSlider != null)
            {
                mSlider.value = mSlider.minValue;
            }
            UpdateValueText(Value);
        }

        private void OnDestroy()
        {
            ValueChanged = null;
            SoundRequested = null;
        }
    }
}
