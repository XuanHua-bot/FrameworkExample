using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 提示消息类型
    /// </summary>
    public enum UIMessageType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// 通用消息提示条 / Toast 展示器组件（P0 表现组件）
    /// 可挂载于 Concave 的 Notification 预制体（如 Notification - Concave - Info / Warning / Error）
    /// 负责弹出提示、自动淡出倒计时、音效解耦请求与对象池重置
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIMessagePresenter : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private CanvasGroup mCanvasGroup;
        [SerializeField] private TMP_Text mMessageText;
        [SerializeField] private Image mTypeIcon;

        [Header("Type Sprites (Optional)")]
        [SerializeField] private Sprite mInfoSprite;
        [SerializeField] private Sprite mSuccessSprite;
        [SerializeField] private Sprite mWarningSprite;
        [SerializeField] private Sprite mErrorSprite;

        [Header("Timing")]
        [Tooltip("默认持续显示时长（秒）")]
        [SerializeField] private float mDefaultDuration = 2.5f;

        [Tooltip("淡入淡出时长（秒）")]
        [SerializeField] private float mFadeDuration = 0.2f;

        [Header("Audio")]
        [SerializeField] private AudioCueKey mShowSound;

        private Coroutine mPlayCoroutine;
        private Action mOnCompleteCallback;

        /// <summary>
        /// 音效请求事件（解耦 IAudioService）
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        /// <summary>
        /// 消息提示展示完成事件
        /// </summary>
        public event Action Completed;

        private void Awake()
        {
            if (mCanvasGroup == null)
            {
                mCanvasGroup = GetComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// 展示一条消息
        /// </summary>
        /// <param name="message">消息文本</param>
        /// <param name="type">消息类型</param>
        /// <param name="duration">持续时长，小于等于0则使用默认配置</param>
        /// <param name="onComplete">完成回调</param>
        public void Show(string message, UIMessageType type = UIMessageType.Info, float duration = -1f, Action onComplete = null)
        {
            if (mMessageText != null)
            {
                mMessageText.text = message;
            }

            UpdateTypeVisual(type);

            mOnCompleteCallback = onComplete;
            float stayTime = duration > 0f ? duration : mDefaultDuration;

            if (mPlayCoroutine != null)
            {
                StopCoroutine(mPlayCoroutine);
            }

            gameObject.SetActive(true);

            if (mShowSound.IsValid)
            {
                SoundRequested?.Invoke(mShowSound);
            }

            mPlayCoroutine = StartCoroutine(ShowRoutine(stayTime));
        }

        private void UpdateTypeVisual(UIMessageType type)
        {
            if (mTypeIcon == null) return;

            Sprite targetSprite = null;
            switch (type)
            {
                case UIMessageType.Info: targetSprite = mInfoSprite; break;
                case UIMessageType.Success: targetSprite = mSuccessSprite; break;
                case UIMessageType.Warning: targetSprite = mWarningSprite; break;
                case UIMessageType.Error: targetSprite = mErrorSprite; break;
            }

            if (targetSprite != null)
            {
                mTypeIcon.sprite = targetSprite;
                mTypeIcon.gameObject.SetActive(true);
            }
        }

        private IEnumerator ShowRoutine(float duration)
        {
            // 1. 淡入
            if (mCanvasGroup != null)
            {
                mCanvasGroup.blocksRaycasts = false;
                float elapsed = 0f;
                while (elapsed < mFadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    mCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / mFadeDuration);
                    yield return null;
                }
                mCanvasGroup.alpha = 1f;
            }

            // 2. 驻留
            yield return new WaitForSecondsRealtime(duration);

            // 3. 淡出
            if (mCanvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < mFadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    mCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / mFadeDuration);
                    yield return null;
                }
                mCanvasGroup.alpha = 0f;
            }

            gameObject.SetActive(false);
            mPlayCoroutine = null;

            var callback = mOnCompleteCallback;
            mOnCompleteCallback = null;
            callback?.Invoke();
            Completed?.Invoke();
        }

        /// <summary>
        /// 隐藏并终止播放
        /// </summary>
        public void HideImmediate()
        {
            if (mPlayCoroutine != null)
            {
                StopCoroutine(mPlayCoroutine);
                mPlayCoroutine = null;
            }

            if (mCanvasGroup != null)
            {
                mCanvasGroup.alpha = 0f;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            HideImmediate();
            mOnCompleteCallback = null;
            SoundRequested = null;
            Completed = null;

            if (mMessageText != null)
            {
                mMessageText.text = string.Empty;
            }
        }

        private void OnDestroy()
        {
            HideImmediate();
            mOnCompleteCallback = null;
            SoundRequested = null;
            Completed = null;
        }
    }
}
