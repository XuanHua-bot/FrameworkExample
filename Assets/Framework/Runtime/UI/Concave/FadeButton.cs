// Copyright (C) 2015-2021 gamevanilla - All rights reserved.
// This code can only be used under the standard Unity Asset Store End User License Agreement.
// A Copy of the Asset Store EULA is available at http://unity3d.com/company/legal/as_terms.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameFramework.Concave
{
    /// <summary>
    /// This button extends Unity's Button and smoothly fades the button in/out
    /// as the user rolls over or presses it.
    /// </summary>
    [RequireComponent(typeof(FadeConfig))]
    [RequireComponent(typeof(CanvasGroup))]
    public class FadeButton : Button
    {
        private FadeConfig config;
        private CanvasGroup canvasGroup;

        protected override void Awake()
        {
            base.Awake();
            config = GetComponent<FadeConfig>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (config == null || canvasGroup == null) return;
            StopAllCoroutines();
            StartCoroutine(Utils.FadeOut(canvasGroup, config.onHoverAlpha, config.fadeTime));
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (config == null || canvasGroup == null) return;
            StopAllCoroutines();
            StartCoroutine(Utils.FadeIn(canvasGroup, 1.0f, config.fadeTime));
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (config == null || canvasGroup == null) return;
            canvasGroup.alpha = config.onClickAlpha;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            if (config == null || canvasGroup == null) return;
            canvasGroup.alpha = 1.0f;
        }
    }
}
