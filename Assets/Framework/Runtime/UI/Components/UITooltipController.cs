using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 全局/局部通用 Tooltip 控制器（P3 表现组件）
    /// 支持动态文本与富文本、智能屏幕边缘防遮挡检测（Screen Boundary Clamp）、淡入缓动动效
    /// 完美契合 Concave 的 Feedback/Tooltips 系列预制体（如 Tooltip - Bottom, Tooltip - Top 等）
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class UITooltipController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasGroup mCanvasGroup;
        [SerializeField] private RectTransform mTooltipRect;
        [SerializeField] private TMP_Text mContentText;
        [SerializeField] private TMP_Text mTitleText;

        [Header("Appearance & Behavior")]
        [Tooltip("鼠标或目标位置的偏移量")]
        [SerializeField] private Vector2 mOffset = new Vector2(15f, -15f);

        [Tooltip("屏幕边缘内缩保护间距")]
        [SerializeField] private Vector2 mScreenPadding = new Vector2(20f, 20f);

        [Tooltip("显示淡入时长（秒）")]
        [SerializeField] private float mFadeDuration = 0.15f;

        // 运行时状态
        private Coroutine mFadeCoroutine;
        private Canvas mRootCanvas;
        private bool mIsVisible;

        public bool IsVisible => mIsVisible;

        private void Awake()
        {
            if (mCanvasGroup == null)
            {
                mCanvasGroup = GetComponent<CanvasGroup>();
            }

            if (mTooltipRect == null)
            {
                mTooltipRect = GetComponent<RectTransform>();
            }

            mRootCanvas = GetComponentInParent<Canvas>();

            HideImmediate();
        }

        /// <summary>
        /// 显示 Tooltip
        /// </summary>
        /// <param name="content">主体提示内容</param>
        /// <param name="title">标题内容（可选）</param>
        /// <param name="screenPosition">触发屏幕坐标（通常为 Input.mousePosition 或 UI 元素中心点）</param>
        public void Show(string content, string title = null, Vector2? screenPosition = null)
        {
            if (string.IsNullOrEmpty(content))
            {
                Hide();
                return;
            }

            mIsVisible = true;

            if (mContentText != null)
            {
                mContentText.text = content;
            }

            if (mTitleText != null)
            {
                mTitleText.text = title ?? string.Empty;
                mTitleText.gameObject.SetActive(!string.IsNullOrEmpty(title));
            }

            gameObject.SetActive(true);

            // 强制刷新 ContentSizeFitter 尺寸计算
            LayoutRebuilder.ForceRebuildLayoutImmediate(mTooltipRect);

            // 设定并自适应位置
            Vector2 targetPos = screenPosition.HasValue ? screenPosition.Value : (Vector2)Input.mousePosition;
            SetPosition(targetPos);

            // 播放淡入
            if (mFadeCoroutine != null)
            {
                StopCoroutine(mFadeCoroutine);
            }
            mFadeCoroutine = StartCoroutine(FadeRoutine(1f));
        }

        /// <summary>
        /// 隐藏 Tooltip
        /// </summary>
        public void Hide()
        {
            if (!mIsVisible) return;
            mIsVisible = false;

            if (mFadeCoroutine != null)
            {
                StopCoroutine(mFadeCoroutine);
            }
            mFadeCoroutine = StartCoroutine(FadeRoutine(0f, onComplete: () => {
                gameObject.SetActive(false);
            }));
        }

        public void HideImmediate()
        {
            mIsVisible = false;
            if (mFadeCoroutine != null)
            {
                StopCoroutine(mFadeCoroutine);
                mFadeCoroutine = null;
            }

            if (mCanvasGroup != null)
            {
                mCanvasGroup.alpha = 0f;
                mCanvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 屏幕边缘自适应检测并定位
        /// </summary>
        public void SetPosition(Vector2 screenPosition)
        {
            if (mTooltipRect == null) return;

            Vector2 finalPos = screenPosition + mOffset;

            // 获取 Tooltip 尺寸
            Vector2 size = mTooltipRect.sizeDelta;
            float screenW = Screen.width;
            float screenH = Screen.height;

            // 右边界超出：向左翻转
            if (finalPos.x + size.x > screenW - mScreenPadding.x)
            {
                finalPos.x = screenPosition.x - size.x - Mathf.Abs(mOffset.x);
            }

            // 下边界超出：向上翻转
            if (finalPos.y - size.y < mScreenPadding.y)
            {
                finalPos.y = screenPosition.y + size.y + Mathf.Abs(mOffset.y);
            }

            // 左上边界保护
            finalPos.x = Mathf.Max(mScreenPadding.x, finalPos.x);
            finalPos.y = Mathf.Min(screenH - mScreenPadding.y, finalPos.y);

            // 转化为 Canvas 局部坐标或直接赋屏幕坐标
            if (mRootCanvas != null && mRootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)mTooltipRect.parent,
                    finalPos,
                    mRootCanvas.worldCamera,
                    out Vector2 localPoint
                );
                mTooltipRect.anchoredPosition = localPoint;
            }
            else
            {
                mTooltipRect.position = finalPos;
            }
        }

        private IEnumerator FadeRoutine(float targetAlpha, Action onComplete = null)
        {
            if (mCanvasGroup == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            float startAlpha = mCanvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < mFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                mCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / mFadeDuration);
                yield return null;
            }

            mCanvasGroup.alpha = targetAlpha;
            mFadeCoroutine = null;
            onComplete?.Invoke();
        }

        public virtual void ResetForPool()
        {
            HideImmediate();
        }

        private void OnDestroy()
        {
            HideImmediate();
        }
    }
}
