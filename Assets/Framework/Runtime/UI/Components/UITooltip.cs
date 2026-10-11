using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace GameFramework
{
    //接受 调用方传入的文本
    //控制CanvasGroup显示与隐藏
    //播放渐入渐出动画
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UITooltip:MonoBehaviour
    {
        [SerializeField]private TMP_Text mTitle;
        [SerializeField]private TMP_Text mBody;

        [SerializeField]private float mFadeDuration = 0.12f;

        private CanvasGroup mCanvasGroup;
        private Tween mFadeTween;
        
        
        public bool IsVisible { get; private set; }

        private void Awake()
        {
            mCanvasGroup = GetComponent<CanvasGroup>();
            HideImmediate();
        }

        private void OnDestroy()
        {
            mFadeTween?.Kill();
        }

        public void SetContent(string title, string body)
        {
            if (mTitle!=null)
            {
                mTitle.text = title ?? string.Empty;
                mTitle.gameObject.SetActive(!string.IsNullOrEmpty(title));
            }

            if (mBody!=null)
            {
                mBody.text = body ?? string.Empty;
            }
        }
        /// <summary>
        /// 设置内容并显示Tooltip
        /// Tooltip的位置由 对应方向Prefab的 RectTransform和调用方决定
        /// </summary>
        /// <param name="title"></param>
        /// <param name="body"></param>
        public void Show(string title, string body)
        {
            SetContent(title,body);
            Show();
        }

        public void Show()
        {
            mFadeTween?.Kill();
            IsVisible = true;
            gameObject.SetActive(true);
            mCanvasGroup.blocksRaycasts = false;
            mCanvasGroup.interactable = false;

            mFadeTween = mCanvasGroup.DOFade(1, mFadeDuration).SetUpdate(true);
        }

        /// <summary>
        /// 隐藏Tooltip 保留示例
        /// </summary>
        public void Hide()
        {
            if (!gameObject.activeSelf)
            {
                IsVisible = false;
                return;
            }
            mFadeTween?.Kill();
            IsVisible = false;
            mCanvasGroup.blocksRaycasts = false;
            mCanvasGroup.interactable = false;

            mFadeTween = mCanvasGroup.DOFade(0, mFadeDuration).SetUpdate(true)
                .OnComplete(() => gameObject.SetActive(false));
        }

        private void HideImmediate()
        {
            mFadeTween?.Kill();
            IsVisible = false;
            mCanvasGroup.alpha = 0f;
            mCanvasGroup.blocksRaycasts = false;
            mCanvasGroup.interactable = false;
            gameObject.SetActive(false);
        }
        
    }
}