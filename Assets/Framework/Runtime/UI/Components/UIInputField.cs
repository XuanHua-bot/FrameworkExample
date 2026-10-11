using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 统一输入框组件（P2 表现组件）
    /// 封装 TMP_InputField，提供防抖输入事件（Debounce）、清空按钮、字符数限制统计及解耦音效
    /// 完美契合 Concave 的 InputField 系列预制体
    /// </summary>
    [DisallowMultipleComponent]
    public class UIInputField : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TMP_InputField mInputField;

        [Tooltip("字符统计文本（可选，格式如 5/20）")]
        [SerializeField] private TMP_Text mCharacterCountText;

        [Tooltip("一键清空按钮（可选）")]
        [SerializeField] private UIButton mClearButton;

        [Header("Debounce & Formatting")]
        [Tooltip("输入内容变更防抖延迟（秒），0 表示每按键立即通知，建议 0.2s 适配搜索框实时查询")]
        [SerializeField] private float mDebounceSeconds = 0.2f;

        [Header("Audio")]
        [SerializeField] private AudioCueKey mTypeSound;
        [SerializeField] private AudioCueKey mSubmitSound;

        // 运行时状态
        private Coroutine mDebounceCoroutine;
        private string mLastNotifiedText = string.Empty;

        public string Text
        {
            get => mInputField != null ? mInputField.text : string.Empty;
            set => SetText(value);
        }

        public bool Interactable
        {
            get => mInputField != null && mInputField.interactable;
            set
            {
                if (mInputField != null)
                {
                    mInputField.interactable = value;
                }
            }
        }

        public void SetTypeSound(AudioCueKey soundKey) => mTypeSound = soundKey;
        public void SetSubmitSound(AudioCueKey soundKey) => mSubmitSound = soundKey;

        /// <summary>
        /// 输入文本变更事件（防抖处理后分发）
        /// </summary>
        public event Action<string> ValueChanged;

        /// <summary>
        /// 回车或完成输入提交事件
        /// </summary>
        public event Action<string> Submitted;

        /// <summary>
        /// 音效请求事件（解耦 IAudioService）
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        private void Awake()
        {
            if (mInputField == null)
            {
                mInputField = GetComponent<TMP_InputField>();
                if (mInputField == null)
                {
                    var inputs = GetComponentsInChildren<TMP_InputField>(true);
                    if (inputs.Length == 1) mInputField = inputs[0];
                }
            }

            if (mClearButton != null)
            {
                mClearButton.Clicked += Clear;
            }
        }

        private void OnEnable()
        {
            if (mInputField != null)
            {
                mInputField.onValueChanged.AddListener(HandleRawValueChanged);
                mInputField.onSubmit.AddListener(HandleRawSubmit);
            }
            UpdateCharacterCount();
        }

        private void OnDisable()
        {
            if (mInputField != null)
            {
                mInputField.onValueChanged.RemoveListener(HandleRawValueChanged);
                mInputField.onSubmit.RemoveListener(HandleRawSubmit);
            }

            if (mDebounceCoroutine != null)
            {
                StopCoroutine(mDebounceCoroutine);
                mDebounceCoroutine = null;
            }
        }

        public void SetText(string text)
        {
            if (mDebounceCoroutine != null)
            {
                StopCoroutine(mDebounceCoroutine);
                mDebounceCoroutine = null;
            }

            if (mInputField != null)
            {
                mInputField.SetTextWithoutNotify(text ?? string.Empty);
            }
            mLastNotifiedText = text ?? string.Empty;
            UpdateCharacterCount();
        }

        public void Clear()
        {
            SetText(string.Empty);
            ValueChanged?.Invoke(string.Empty);
        }

        private void HandleRawValueChanged(string newText)
        {
            UpdateCharacterCount();

            if (mTypeSound.IsValid)
            {
                SoundRequested?.Invoke(mTypeSound);
            }

            if (mDebounceSeconds <= 0f)
            {
                mLastNotifiedText = newText;
                ValueChanged?.Invoke(newText);
                return;
            }

            if (mDebounceCoroutine != null)
            {
                StopCoroutine(mDebounceCoroutine);
            }
            mDebounceCoroutine = StartCoroutine(DebounceRoutine(newText));
        }

        private IEnumerator DebounceRoutine(string newText)
        {
            yield return new WaitForSecondsRealtime(mDebounceSeconds);
            mDebounceCoroutine = null;

            if (mLastNotifiedText != newText)
            {
                mLastNotifiedText = newText;
                ValueChanged?.Invoke(newText);
            }
        }

        private void HandleRawSubmit(string text)
        {
            if (mSubmitSound.IsValid)
            {
                SoundRequested?.Invoke(mSubmitSound);
            }
            Submitted?.Invoke(text);
        }

        private void UpdateCharacterCount()
        {
            if (mCharacterCountText == null || mInputField == null) return;

            int current = mInputField.text.Length;
            int max = mInputField.characterLimit;

            if (max > 0)
            {
                mCharacterCountText.text = $"{current}/{max}";
            }
            else
            {
                mCharacterCountText.text = current.ToString();
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            if (mDebounceCoroutine != null)
            {
                StopCoroutine(mDebounceCoroutine);
                mDebounceCoroutine = null;
            }

            SetText(string.Empty);
            ValueChanged = null;
            Submitted = null;
            SoundRequested = null;
        }

        private void OnDestroy()
        {
            if (mClearButton != null)
            {
                mClearButton.Clicked -= Clear;
            }

            ValueChanged = null;
            Submitted = null;
            SoundRequested = null;
        }
    }
}
