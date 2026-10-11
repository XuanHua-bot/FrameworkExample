using System;
using UnityEngine;
using UnityEngine.UI;


namespace GameFramework
{
    /// <summary>
    /// 所有ui页面必须继承此类
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPage:MonoBehaviour
    {
        [Header("Input")]
        [SerializeField]
        private bool mBlocksUnderlyingInput;

        [SerializeField]
        private GameObject mInputBlocker;

        [Header("Interaction")]
        [SerializeField]
        private CanvasGroup mCanvasGroup;

        protected virtual void Awake()
        {
           

            if (mCanvasGroup == null)
            {
                mCanvasGroup = GetComponent<CanvasGroup>();
            }

            // RequireComponent covers normal Inspector setup; this also repairs older prefabs at runtime.
            if (mCanvasGroup == null)
            {
                mCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            EnsureInputBlocker();
        }

        protected virtual void Reset()
        {
            mCanvasGroup = GetComponent<CanvasGroup>();
        }

        public virtual void SetInteractable(bool interactable)
        {
            if (mCanvasGroup==null)return;
            mCanvasGroup.interactable = interactable;
            mCanvasGroup.blocksRaycasts = interactable;
        }

        /// <summary>
        /// 页面显示时是否阻挡页面下方的 UGUI 输入。
        /// 这个配置独立于 UILayer；Normal 层页面也可以开启遮挡。
        /// </summary>
        public bool BlocksUnderlyingInput => mBlocksUnderlyingInput;

        /// <summary>
        /// 开关页面 Prefab 自带的全屏透明遮挡对象。
        /// InputBlocker 应该位于页面内容下方，并开启 Graphic 的 Raycast Target。
        /// </summary>
        public virtual void SetInputBlocker(bool active)
        {
            EnsureInputBlocker();
            if (mInputBlocker == null)
            {
                return;
            }

            mInputBlocker.SetActive(active && mBlocksUnderlyingInput);
        }

        private void EnsureInputBlocker()
        {
            if(!mBlocksUnderlyingInput||mInputBlocker!=null)return;
            var blocker = new GameObject("InputBlocker",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            var blockerParent = transform.parent != null ? transform.parent : transform;
            blocker.transform.SetParent(blockerParent,false);
            if (blockerParent==transform)
            {
                blockerParent.transform.SetAsFirstSibling();
            }
            else
            {
                blocker.transform.SetSiblingIndex(transform.GetSiblingIndex());
            }

            var rect = blocker.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = blocker.GetComponent<Image>();
            image.color = new Color(0, 0, 0, 0);
            image.raycastTarget = true;
            mInputBlocker = blocker;
            blocker.SetActive(false);
        }

        /// <summary>
        /// 用于初始化
        /// UI Service 创建实例 并 完成登记 后进行调用
        //具体页面在此处 主次BinableProperty 初始化按钮和文本
        /// </summary>
        public virtual void OnOpen() { }
        /// <summary>
        /// 用于 刷新显示
        /// 页面变为可见时进行调用
        /// </summary>
        public virtual void OnShow() { }
        /// <summary>
        /// 用于 暂停 仅在可见运行的动画或者输入
        /// 页面被 其他页面
        /// 覆盖时隐藏
        /// </summary>
        public virtual void OnHide() { }
        /// <summary>
        /// 页面被上层页面覆盖时 调用, 实例仍然存在
        /// </summary>
        public virtual void OnPause(){}
        /// <summary>
        /// 野蛮重新成为顶部页面时调用
        /// </summary>
        public virtual void OnResume(){}
        /// <summary>
        /// 用于 解绑事件
        /// UIService 销毁实例 前 调用
        /// 具体页面 在此注销事件和BindableProperty 回调
        /// </summary>
        public virtual void OnClose() { }

        protected virtual void OnDestroy()
        {
            if (mInputBlocker!=null && mInputBlocker.transform.parent!=transform)
            {
                Destroy(mInputBlocker);
            }
        }
    }
}
