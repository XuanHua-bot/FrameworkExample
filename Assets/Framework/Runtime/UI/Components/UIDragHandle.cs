using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 统一 UI 拖拽源组件（P2 表现组件）
    /// 挂载于可拖拽物体（如背包物品格子、快捷栏技能图标）
    /// 只负责收集和转发拖拽交互与载荷数据（Payload），能否装备、出售或移动交由 Controller 和 Command 决定
    /// </summary>
    [DisallowMultipleComponent]
    public class UIDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Drag Visual (Optional)")]
        [Tooltip("可选拖拽对象；页面设置拖拽图标时优先显示图标副本，均未设置时移动自身")]
        [SerializeField] private RectTransform mDragAvatar;

        [Tooltip("拖拽时是否降低自身透明度")]
        [SerializeField] private CanvasGroup mCanvasGroup;
        [SerializeField] private float mDraggingAlpha = 0.6f;

        [Header("Audio")]
        [SerializeField] private AudioCueKey mBeginDragSound;
        [SerializeField] private AudioCueKey mEndDragSound;

        // 运行时状态
        private bool mInteractable = true;
        private object mPayload;
        private Transform mOriginalParent;
        private Vector3 mOriginalPosition;
        private int mOriginalSiblingIndex;
        private bool mIsDragging;
        private Sprite mDragIcon;
        private GameObject mDragGhost;
        private bool mDropAccepted;
        private float mOriginalAlpha;
        private bool mOriginalBlocksRaycasts;
        private bool mVisualChanged;

        public bool IsDragging => mIsDragging;
        public object Payload => mPayload;

        public bool Interactable
        {
            get => mInteractable;
            set => mInteractable = value;
        }

        /// <summary>
        /// 开始拖拽事件 (handle, payload)
        /// </summary>
        public event Action<UIDragHandle, object> BeginDragEvent;

        /// <summary>
        /// 拖拽移动事件 (handle, pointerPosition)
        /// </summary>
        public event Action<UIDragHandle, Vector2> DragEvent;

        /// <summary>
        /// 结束拖拽事件 (handle, payload, isSuccessDrop)
        /// </summary>
        public event Action<UIDragHandle, object, bool> EndDragEvent;

        /// <summary>
        /// 音效请求事件（解耦 IAudioService）
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        private void Awake()
        {
            if (mCanvasGroup == null)
            {
                mCanvasGroup = GetComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// 设置当前拖拽承载的数据对象
        /// </summary>
        public void SetPayload(object payload)
        {
            mPayload = payload;
        }

        // 列表项保持原位，拖动时只显示图标副本；不设置时沿用普通 UI 的拖动方式。
        public void SetDragIcon(Sprite icon) => mDragIcon = icon;

        public void SetDropResult(bool accepted)
        {
            if (mIsDragging) mDropAccepted = accepted;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!mInteractable || !isActiveAndEnabled) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (mDragIcon != null && canvas == null) return;
            mIsDragging = true;
            mDropAccepted = false;
            if (mDragIcon != null)
            {
                mDragGhost = new GameObject("Drag Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                mDragGhost.transform.SetParent(canvas.transform, false);
                mDragGhost.transform.SetAsLastSibling();
                var image = mDragGhost.GetComponent<Image>();
                image.sprite = mDragIcon;
                image.preserveAspect = true;
                image.raycastTarget = false;
                ((RectTransform)mDragGhost.transform).sizeDelta = new Vector2(64, 64);
                PositionGhost(eventData, canvas);
                if (mCanvasGroup == null) mCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            else
            {
                var targetRect = mDragAvatar != null ? mDragAvatar : (RectTransform)transform;
                mOriginalParent = targetRect.parent;
                mOriginalPosition = targetRect.position;
                mOriginalSiblingIndex = targetRect.GetSiblingIndex();
            }

            if (mCanvasGroup != null)
            {
                mOriginalAlpha = mCanvasGroup.alpha;
                mOriginalBlocksRaycasts = mCanvasGroup.blocksRaycasts;
                mVisualChanged = true;
                mCanvasGroup.alpha = mDraggingAlpha;
                mCanvasGroup.blocksRaycasts = false; // 允许穿透检测下方的 DropTarget
            }

            if (mBeginDragSound.IsValid)
            {
                SoundRequested?.Invoke(mBeginDragSound);
            }

            BeginDragEvent?.Invoke(this, mPayload);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!mIsDragging) return;

            if (mDragGhost != null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) PositionGhost(eventData, canvas.rootCanvas);
            }
            else
            {
                var targetRect = mDragAvatar != null ? mDragAvatar : (RectTransform)transform;
                targetRect.position = eventData.position;
            }

            DragEvent?.Invoke(this, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!mIsDragging) return;

            mIsDragging = false;

            bool isSuccessDrop = mDropAccepted;
            ResetDragVisual();

            if (mEndDragSound.IsValid)
            {
                SoundRequested?.Invoke(mEndDragSound);
            }

            EndDragEvent?.Invoke(this, mPayload, isSuccessDrop);
        }

        public void ResetDragVisual()
        {
            if (mDragGhost != null)
            {
                Destroy(mDragGhost);
                mDragGhost = null;
            }
            else if (mOriginalParent != null)
            {
                var targetRect = mDragAvatar != null ? mDragAvatar : (RectTransform)transform;
                targetRect.SetParent(mOriginalParent);
                targetRect.position = mOriginalPosition;
                targetRect.SetSiblingIndex(mOriginalSiblingIndex);
            }
            mOriginalParent = null;

            if (mCanvasGroup != null && mVisualChanged)
            {
                mCanvasGroup.alpha = mOriginalAlpha;
                mCanvasGroup.blocksRaycasts = mOriginalBlocksRaycasts;
                mVisualChanged = false;
            }
        }

        private void PositionGhost(PointerEventData eventData, Canvas canvas)
        {
            var rect = (RectTransform)canvas.transform;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, eventData.position, camera, out var position))
                mDragGhost.transform.position = position;
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            ResetDragVisual();
            mIsDragging = false;
            mInteractable = true;
            mPayload = null;
            mDragIcon = null;
            mDropAccepted = false;

            BeginDragEvent = null;
            DragEvent = null;
            EndDragEvent = null;
            SoundRequested = null;
        }

        private void OnDestroy()
        {
            ResetDragVisual();
            BeginDragEvent = null;
            DragEvent = null;
            EndDragEvent = null;
            SoundRequested = null;
        }

        private void OnDisable()
        {
            if (!mIsDragging) return;
            ResetDragVisual();
            mIsDragging = false;
            mDropAccepted = false;
        }
    }
}
