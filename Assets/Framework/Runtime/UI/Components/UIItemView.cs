using TMPro;
using UnityEngine;

namespace GameFramework
{
    /// <summary>只展示内容，业务数据的映射由页面负责。</summary>
    [DisallowMultipleComponent]
    public sealed class UIItemView : MonoBehaviour
    {
        [SerializeField] private UIItemIcon mIcon;
        [SerializeField] private UIItemCount mCount;
        [SerializeField] private TMP_Text mTitle;

        // 每次覆盖全部字段，滚动复用时不会残留上一个条目的状态。
        public void SetContent(string title, Sprite icon, long count = 1, bool locked = false,
            Sprite qualityBorder = null, Color? qualityColor = null)
        {
            if (mTitle != null) mTitle.text = title ?? string.Empty;
            if (mCount != null) mCount.SetCount(count);
            if (mIcon == null) return;
            mIcon.SetIcon(icon);
            mIcon.SetLocked(locked);
            mIcon.SetQualityBorder(qualityBorder, qualityColor ?? Color.white);
        }

        public void ResetForPool() => SetContent(string.Empty, null, 0);
    }
}
