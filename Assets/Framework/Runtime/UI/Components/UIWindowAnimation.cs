using System;
using System.Collections;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 窗口弹窗过渡动画类型
    /// </summary>
    public enum UIWindowAnimationType
    {
        None,
        ScaleFade,   // 弹性缩放 + 渐变淡出
        FadeOnly,    // 纯透明度渐变
        SlideBottom, // 从底部滑入
        SlideTop     // 从顶部滑入
    }

    /// <summary>
    /// 统一弹窗与窗口动效组件（P3 表现组件）
    /// 为 Popup、Panel 提供进入 (Show) 和退出 (Hide) 的平滑缓动动画
    /// 支持即时中断、独立生命周期保护及动画结束回调
    /// </summary>
    [DisallowMultipleComponent]
    public class UIWindowAnimation : MonoBehaviour
    {
        [Header("Animation Config")]
        [SerializeField] private UIWindowAnimationType mAnimationType = UIWindowAnimationType.ScaleFade;
        [SerializeField] private float mDuration = 0.2f;

        [Header("Targets")]
        [Tooltip("受动画影响的 RectTransform（留空默认为自身）")]
        [SerializeField] private RectTransform mTargetRect;

        [Tooltip("受渐变影响的 CanvasGroup（留空自动获取或添加）")]
        [SerializeField] private CanvasGroup mCanvasGroup;

        [Header("Audio")]
        [SerializeField] private AudioCueKey mOpenSound;
        [SerializeField] private AudioCueKey mCloseSound;

        private Coroutine mAnimationCoroutine;
        private Vector2 mOriginalAnchoredPos;
        private Vector3 mOriginalScale = Vector3.one;
        private bool mIsInitialized;

        public bool IsPlaying => mAnimationCoroutine != null;

        /// <summary>
        /// 音效请求事件（解耦 IAudioService）
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        private void Awake()
        {
            EnsureInit();
        }

        private void EnsureInit()
        {
            if (mIsInitialized) return;
            mIsInitialized = true;

            if (mTargetRect == null)
            {
                mTargetRect = GetComponent<RectTransform>();
            }

            if (mCanvasGroup == null)
            {
                mCanvasGroup = GetComponent<CanvasGroup>();
                if (mCanvasGroup == null)
                {
                    mCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (mTargetRect != null)
            {
                mOriginalAnchoredPos = mTargetRect.anchoredPosition;
                mOriginalScale = mTargetRect.localScale;
            }
        }

        /// <summary>
        /// 播放打开动画
        /// </summary>
        public void PlayOpen(Action onComplete = null)
        {
            EnsureInit();

            if (mAnimationCoroutine != null)
            {
                StopCoroutine(mAnimationCoroutine);
            }

            if (mOpenSound.IsValid)
            {
                SoundRequested?.Invoke(mOpenSound);
            }

            gameObject.SetActive(true);

            if (mAnimationType == UIWindowAnimationType.None || mDuration <= 0f || !isActiveAndEnabled)
            {
                ApplyNormalState();
                onComplete?.Invoke();
                return;
            }

            mAnimationCoroutine = StartCoroutine(OpenRoutine(onComplete));
        }

        /// <summary>
        /// 播放关闭动画
        /// </summary>
        public void PlayClose(Action onComplete = null)
        {
            EnsureInit();

            if (mAnimationCoroutine != null)
            {
                StopCoroutine(mAnimationCoroutine);
            }

            if (mCloseSound.IsValid)
            {
                SoundRequested?.Invoke(mCloseSound);
            }

            if (mAnimationType == UIWindowAnimationType.None || mDuration <= 0f || !isActiveAndEnabled)
            {
                ApplyClosedState();
                gameObject.SetActive(false);
                onComplete?.Invoke();
                return;
            }

            mAnimationCoroutine = StartCoroutine(CloseRoutine(() => {
                gameObject.SetActive(false);
                onComplete?.Invoke();
            }));
        }

        private IEnumerator OpenRoutine(Action onComplete)
        {
            float elapsed = 0f;

            while (elapsed < mDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / mDuration);
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f); // EaseOutSin

                ApplyProgress(easeOut, isOpen: true);
                yield return null;
            }

            ApplyNormalState();
            mAnimationCoroutine = null;
            onComplete?.Invoke();
        }

        private IEnumerator CloseRoutine(Action onComplete)
        {
            float elapsed = 0f;

            while (elapsed < mDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / mDuration);
                float easeIn = 1f - Mathf.Cos(t * Mathf.PI * 0.5f); // EaseInSin

                ApplyProgress(1f - easeIn, isOpen: false);
                yield return null;
            }

            ApplyClosedState();
            mAnimationCoroutine = null;
            onComplete?.Invoke();
        }

        private void ApplyProgress(float p, bool isOpen)
        {
            if (mCanvasGroup != null)
            {
                mCanvasGroup.alpha = p;
            }

            if (mTargetRect == null) return;

            switch (mAnimationType)
            {
                case UIWindowAnimationType.ScaleFade:
                    // 0.8 到 1.0 的弹性回正
                    float scaleFactor = Mathf.Lerp(0.85f, 1f, p);
                    mTargetRect.localScale = mOriginalScale * scaleFactor;
                    break;

                case UIWindowAnimationType.SlideBottom:
                    float offsetBottom = (1f - p) * -300f;
                    mTargetRect.anchoredPosition = mOriginalAnchoredPos + new Vector2(0f, offsetBottom);
                    break;

                case UIWindowAnimationType.SlideTop:
                    float offsetTop = (1f - p) * 300f;
                    mTargetRect.anchoredPosition = mOriginalAnchoredPos + new Vector2(0f, offsetTop);
                    break;

                case UIWindowAnimationType.FadeOnly:
                case UIWindowAnimationType.None:
                    break;
            }
        }

        public void ApplyNormalState()
        {
            if (mCanvasGroup != null)
            {
                mCanvasGroup.alpha = 1f;
            }

            if (mTargetRect != null)
            {
                mTargetRect.localScale = mOriginalScale;
                mTargetRect.anchoredPosition = mOriginalAnchoredPos;
            }
        }

        public void ApplyClosedState()
        {
            if (mCanvasGroup != null)
            {
                mCanvasGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            if (mAnimationCoroutine != null)
            {
                StopCoroutine(mAnimationCoroutine);
                mAnimationCoroutine = null;
            }

            ApplyNormalState();
            SoundRequested = null;
        }

        private void OnDestroy()
        {
            if (mAnimationCoroutine != null)
            {
                StopCoroutine(mAnimationCoroutine);
                mAnimationCoroutine = null;
            }
            SoundRequested = null;
        }
    }
}
