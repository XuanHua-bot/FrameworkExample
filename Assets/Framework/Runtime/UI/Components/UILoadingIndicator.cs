using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 转圈加载指示器组件
    /// 提供两种样式：单图旋转（Rotate）与 Concave 风格点阵转圈（DotsRing，纯代码动画）
    /// 以及淡入淡出、提示文本更新与超时安全保护
    /// </summary>
    [DisallowMultipleComponent]
    public class UILoadingIndicator : MonoBehaviour
    {
        [Header("Spinner Animation")]
        [Tooltip("需要旋转的物体或图标（留空则默认旋转自身的 transform）")]
        [SerializeField] private RectTransform mSpinner;

        [Tooltip("每秒旋转的角度（顺时针为负，逆时针为正，通常负值如 -360）")]
        [SerializeField] private float mRotationSpeed = -360f;

        [Header("Text & Details (Optional)")]
        [SerializeField] private TMP_Text mTipText;

        [Header("Canvas Group Fade (Optional)")]
        [SerializeField] private CanvasGroup mCanvasGroup;
        [SerializeField] private float mFadeDuration = 0.15f;

        [Header("Style")]
        [Tooltip("Rotate：单图旋转（默认）；DotsRing：Concave 风格点阵转圈（纯代码动画，无需 Animator）")]
        [SerializeField] private SpinnerStyle mStyle = SpinnerStyle.Rotate;

        [Header("Dots Ring Style")]
        [Tooltip("点阵使用的贴图（例如 Concave 形状方块），留空则不显示")]
        [SerializeField] private Sprite mDotSprite;
        [SerializeField, Min(3)] private int mDotCount = 8;
        [SerializeField, Min(0f)] private float mDotRadius = 22f;
        [SerializeField, Min(1f)] private float mDotSize = 10f;
        [SerializeField, Min(0.1f)] private float mDotsCycleSeconds = 1.1f;
        [SerializeField, Range(0f, 1f)] private float mDotMinAlpha = 0.15f;
        [SerializeField] private Color mDotColor = Color.white;

        /// <summary>指示器样式</summary>
        public enum SpinnerStyle
        {
            Rotate,
            DotsRing
        }

        private Coroutine mRotateCoroutine;
        private Coroutine mFadeCoroutine;
        private Coroutine mDotsCoroutine;
        private Image mRootImage;
        private Image[] mDots;
        private bool mIsSpinning;

        public bool IsSpinning => mIsSpinning;

        private void Awake()
        {
            if (mSpinner == null)
            {
                mSpinner = GetComponent<RectTransform>();
            }

            if (mCanvasGroup == null)
            {
                mCanvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            // 首次启用时开始旋转；若是在 StartSpin 之后才被激活（宿主此前未激活），
            // 这里补启协程，避免 "game object is inactive" 报错。
            if (!mIsSpinning)
            {
                StartSpin();
                return;
            }

            if (mStyle == SpinnerStyle.DotsRing)
            {
                if (mDotsCoroutine == null)
                {
                    mDotsCoroutine = StartCoroutine(DotsRoutine());
                }
                return;
            }

            if (mRotateCoroutine == null)
            {
                mRotateCoroutine = StartCoroutine(RotateRoutine());
            }
        }

        private void OnDisable()
        {
            StopSpin();
        }

        /// <summary>
        /// 设置加载提示文案
        /// </summary>
        public void SetTip(string tip)
        {
            if (mTipText != null)
            {
                mTipText.text = tip;
            }
        }

        /// <summary>
        /// 启动旋转动画
        /// </summary>
        public void StartSpin()
        {
            if (mIsSpinning) return;
            mIsSpinning = true;

            if (mCanvasGroup != null)
            {
                mCanvasGroup.alpha = 1f;
            }

            if (mStyle == SpinnerStyle.DotsRing)
            {
                StopRotateRoutine();
                if (mSpinner != null)
                {
                    mSpinner.localRotation = Quaternion.identity;
                }
                BuildOrRefreshDots();
                StopDotsRoutine();
                if (isActiveAndEnabled)
                {
                    mDotsCoroutine = StartCoroutine(DotsRoutine());
                }
                return;
            }

            StopDotsRoutine();
            SetDotsVisible(false);
            if (mRootImage != null)
            {
                mRootImage.enabled = true;
            }

            if (mRotateCoroutine != null)
            {
                StopCoroutine(mRotateCoroutine);
            }

            if (isActiveAndEnabled)
            {
                mRotateCoroutine = StartCoroutine(RotateRoutine());
            }
        }

        /// <summary>
        /// 停止旋转动画
        /// </summary>
        public void StopSpin()
        {
            mIsSpinning = false;
            StopRotateRoutine();
            StopDotsRoutine();
            SetDotsVisible(false);

            if (mFadeCoroutine != null)
            {
                StopCoroutine(mFadeCoroutine);
                mFadeCoroutine = null;
            }
        }

        /// <summary>
        /// 带淡出的停止
        /// </summary>
        public void StopSpinWithFade(Action onComplete = null)
        {
            if (mCanvasGroup == null || mFadeDuration <= 0f)
            {
                StopSpin();
                onComplete?.Invoke();
                return;
            }

            if (mFadeCoroutine != null)
            {
                StopCoroutine(mFadeCoroutine);
            }

            mFadeCoroutine = StartCoroutine(FadeOutRoutine(onComplete));
        }

        private void StopRotateRoutine()
        {
            if (mRotateCoroutine != null)
            {
                StopCoroutine(mRotateCoroutine);
                mRotateCoroutine = null;
            }
        }

        private void StopDotsRoutine()
        {
            if (mDotsCoroutine != null)
            {
                StopCoroutine(mDotsCoroutine);
                mDotsCoroutine = null;
            }
        }

        /// <summary>
        /// 按配置构建（或刷新）点阵。点阵为运行时生成的子物体，不需要在 Prefab 里预先摆好。
        /// </summary>
        private void BuildOrRefreshDots()
        {
            if (mRootImage == null)
            {
                mRootImage = GetComponent<Image>();
            }
            if (mRootImage != null)
            {
                mRootImage.enabled = false;
            }

            var needRebuild = mDots == null || mDots.Length != mDotCount;
            if (!needRebuild)
            {
                foreach (var dot in mDots)
                {
                    if (dot == null)
                    {
                        needRebuild = true;
                        break;
                    }
                }
            }

            if (needRebuild)
            {
                DestroyDots();
                mDots = new Image[Mathf.Max(3, mDotCount)];
                for (var i = 0; i < mDots.Length; i++)
                {
                    var dotObject = new GameObject("Dot " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    var rect = (RectTransform)dotObject.transform;
                    rect.SetParent(transform, false);
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    var image = dotObject.GetComponent<Image>();
                    image.raycastTarget = false;
                    mDots[i] = image;
                }
            }

            for (var i = 0; i < mDots.Length; i++)
            {
                var rect = (RectTransform)mDots[i].transform;
                var angle = Mathf.PI * 2f * i / mDots.Length;
                rect.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * mDotRadius;
                rect.sizeDelta = new Vector2(mDotSize, mDotSize);
                mDots[i].sprite = mDotSprite;
                mDots[i].color = mDotColor;
                mDots[i].gameObject.SetActive(true);
            }
        }

        private void DestroyDots()
        {
            if (mDots == null)
            {
                return;
            }

            foreach (var dot in mDots)
            {
                if (dot != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(dot.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(dot.gameObject);
                    }
                }
            }

            mDots = null;
        }

        private void SetDotsVisible(bool visible)
        {
            if (mDots == null)
            {
                return;
            }

            foreach (var dot in mDots)
            {
                if (dot != null)
                {
                    dot.gameObject.SetActive(visible);
                }
            }
        }

        private IEnumerator DotsRoutine()
        {
            var time = 0f;
            while (true)
            {
                time += Time.unscaledDeltaTime;
                var cycle = Mathf.Max(0.1f, mDotsCycleSeconds);
                for (var i = 0; i < mDots.Length; i++)
                {
                    var dot = mDots[i];
                    if (dot == null)
                    {
                        continue;
                    }

                    var phase = Mathf.Repeat(time / cycle - (float)i / mDots.Length, 1f);
                    var wave = 1f - Mathf.Abs(phase * 2f - 1f);
                    var color = mDotColor;
                    color.a = Mathf.Lerp(mDotMinAlpha, 1f, wave);
                    dot.color = color;
                }
                yield return null;
            }
        }

        private IEnumerator RotateRoutine()
        {
            while (true)
            {
                if (mSpinner != null)
                {
                    mSpinner.Rotate(0f, 0f, mRotationSpeed * Time.unscaledDeltaTime);
                }
                yield return null;
            }
        }

        private IEnumerator FadeOutRoutine(Action onComplete)
        {
            float elapsed = 0f;
            float startAlpha = mCanvasGroup.alpha;

            while (elapsed < mFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                mCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / mFadeDuration);
                yield return null;
            }

            mCanvasGroup.alpha = 0f;
            StopSpin();
            onComplete?.Invoke();
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            StopSpin();
            if (mCanvasGroup != null)
            {
                mCanvasGroup.alpha = 1f;
            }
            if (mRootImage != null)
            {
                mRootImage.enabled = true;
            }
            if (mSpinner != null)
            {
                mSpinner.localRotation = Quaternion.identity;
            }
            if (mTipText != null)
            {
                mTipText.text = string.Empty;
            }
        }
    }
}
