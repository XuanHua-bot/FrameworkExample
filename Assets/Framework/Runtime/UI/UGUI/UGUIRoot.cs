using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// UGUI 页面使用的统一宿主。
    ///
    /// Root 负责保存 Canvas 和各个显示层的引用。
    /// 页面加载、实例登记、生命周期回调和资源释放仍然由 UGUIService 负责。
    /// </summary>
    public sealed class UGUIRoot : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private Canvas mCanvas;

        [Header("Layers")]
        [SerializeField] private Transform mBackgroundLayer;
        [SerializeField] private Transform mNormalLayer;
        [SerializeField] private Transform mPopupLayer;
        [SerializeField] private Transform mSystemLayer;
        [SerializeField] private Transform mToastLayer;

        /// <summary>
        /// Root 使用的 Canvas。
        /// Root 之间的整体绘制顺序由 Canvas 的 sortingOrder 决定。
        /// </summary>
        public Canvas Canvas => mCanvas;

        /// <summary>
        /// 背景层。用于背景图和不负责主要交互的 UI。
        /// </summary>
        public Transform BackgroundLayer => mBackgroundLayer;

        /// <summary>
        /// 普通页面层。用于 HUD、设置页、背包等常规页面。
        /// </summary>
        public Transform NormalLayer => mNormalLayer;

        /// <summary>
        /// 弹窗层。用于确认框、暂停窗口和模态页面。
        /// </summary>
        public Transform PopupLayer => mPopupLayer;

        /// <summary>
        /// 系统层。用于 Loading、断线提示等系统 UI。
        /// </summary>
        public Transform SystemLayer => mSystemLayer;

        /// <summary>
        /// 短时提示层。用于 Toast 等非模态提示。
        /// 高频伤害跳字以后单独使用 FloatingTextLayer，不归入 ToastLayer。
        /// </summary>
        public Transform ToastLayer => mToastLayer;

        public Transform GetLayer(UILayer layer)
        {
            switch (layer)
            {
                case UILayer.Background:
                    return BackgroundLayer;
                case UILayer.Normal:
                    return NormalLayer;
                case UILayer.Popup:
                    return PopupLayer;
                case UILayer.System:
                    return SystemLayer;
                case UILayer.Toast:
                    return ToastLayer;
                default:
                    return null;
            }
            
                
        }
        private void Reset()
        {
            // 添加组件时自动关联同一个 GameObject 上的 Canvas，减少 Inspector 漏配。
            if (mCanvas == null)
            {
                mCanvas = GetComponent<Canvas>();
            }
        }
    }
}
