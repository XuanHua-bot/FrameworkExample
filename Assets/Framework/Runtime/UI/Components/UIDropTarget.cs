using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameFramework
{
    /// <summary>
    /// 统一 UI 拖拽放置目标组件（P2 表现组件）
    /// 挂载于可放置物体的槽位（如快捷栏槽、装备槽、摧毁回收箱、交易栏）
    /// 负责接收上方释放的 UIDragHandle，上报事件供 Controller 校验执行
    /// </summary>
    [DisallowMultipleComponent]
    public class UIDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Target Identity")]
        [Tooltip("槽位或放置目标的业务类型标签（如 'EquipSlot_Weapon', 'TrashBin', 'QuickBar_1'）")]
        [SerializeField] private string mTargetTag = string.Empty;

        [Header("Visual Feedback (Optional)")]
        [Tooltip("当有可放置物体悬停在上方时激活的高亮指示器")]
        [SerializeField] private GameObject mHoverIndicator;

        [Header("Audio")]
        [SerializeField] private AudioCueKey mDropSound;

        // 运行时状态
        private bool mInteractable = true;
        private object mSlotContext;

        public string TargetTag
        {
            get => mTargetTag;
            set => mTargetTag = value;
        }

        public object SlotContext => mSlotContext;

        public bool Interactable
        {
            get => mInteractable;
            set => mInteractable = value;
        }

        /// <summary>
        /// 放置释放事件 (target, dragHandle, payload)
        /// </summary>
        public event Action<UIDropTarget, UIDragHandle, object> Dropped;

        /// <summary>
        /// 拖拽物体移入移出高亮事件 (isHover)
        /// </summary>
        public event Action<bool> HoverChanged;

        /// <summary>
        /// 音效请求事件（解耦 IAudioService）
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        /// <summary>
        /// 设置当前槽位的上下文数据（例如装备槽部位枚举、快捷栏格子序号等）
        /// </summary>
        public void SetSlotContext(object context)
        {
            mSlotContext = context;
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!mInteractable || !isActiveAndEnabled) return;

            var dragObj = eventData.pointerDrag;
            if (dragObj == null) return;

            var dragHandle = dragObj.GetComponent<UIDragHandle>();
            if (dragHandle == null) return;

            if (mHoverIndicator != null)
            {
                mHoverIndicator.SetActive(false);
            }

            if (mDropSound.IsValid)
            {
                SoundRequested?.Invoke(mDropSound);
            }

            Dropped?.Invoke(this, dragHandle, dragHandle.Payload);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!mInteractable) return;

            // 只有当当前正处于有效拖拽状态时才响应高亮
            if (eventData.dragging && eventData.pointerDrag != null)
            {
                if (mHoverIndicator != null)
                {
                    mHoverIndicator.SetActive(true);
                }
                HoverChanged?.Invoke(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (mHoverIndicator != null)
            {
                mHoverIndicator.SetActive(false);
            }
            HoverChanged?.Invoke(false);
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            mInteractable = true;
            mSlotContext = null;
            if (mHoverIndicator != null)
            {
                mHoverIndicator.SetActive(false);
            }

            Dropped = null;
            HoverChanged = null;
            SoundRequested = null;
        }

        private void OnDestroy()
        {
            Dropped = null;
            HoverChanged = null;
            SoundRequested = null;
        }
    }
}
