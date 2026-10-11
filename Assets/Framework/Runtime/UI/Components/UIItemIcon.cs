using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 道具/技能/头像图标组件（P1 表现组件）
    /// 统一管理图标展示、品质边框、灰显（Locked/Disabled）状态及加载占位
    /// </summary>
    [DisallowMultipleComponent]
    public class UIItemIcon : MonoBehaviour
    {
        [Header("Image References")]
        [SerializeField] private Image mIconImage;
        [SerializeField] private Image mQualityBorderImage;
        [SerializeField] private Image mBackgroundImage;

        [Header("Locked / Grayscale")]
        [SerializeField] private GameObject mLockedOverlay;
        [SerializeField] private Color mNormalColor = Color.white;
        [SerializeField] private Color mDisabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        private bool mIsLocked;

        public bool IsLocked => mIsLocked;

        private void Awake()
        {
            if (mIconImage == null)
            {
                mIconImage = GetComponent<Image>();
            }
        }

        /// <summary>
        /// 设置图标 Sprite
        /// </summary>
        public void SetIcon(Sprite icon)
        {
            if (mIconImage != null)
            {
                mIconImage.sprite = icon;
                mIconImage.gameObject.SetActive(icon != null);
            }
        }

        /// <summary>
        /// 设置品质框 Sprite 或颜色
        /// </summary>
        public void SetQualityBorder(Sprite borderSprite, Color? borderColor = null)
        {
            if (mQualityBorderImage != null)
            {
                mQualityBorderImage.sprite = borderSprite;
                if (borderColor.HasValue)
                {
                    mQualityBorderImage.color = borderColor.Value;
                }
                mQualityBorderImage.gameObject.SetActive(borderSprite != null);
            }
        }

        /// <summary>
        /// 设置锁定/置灰状态
        /// </summary>
        public void SetLocked(bool locked)
        {
            mIsLocked = locked;

            if (mLockedOverlay != null)
            {
                mLockedOverlay.SetActive(locked);
            }

            if (mIconImage != null)
            {
                mIconImage.color = locked ? mDisabledColor : mNormalColor;
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            SetLocked(false);
            if (mIconImage != null)
            {
                mIconImage.sprite = null;
                mIconImage.color = mNormalColor;
            }
            if (mQualityBorderImage != null)
            {
                mQualityBorderImage.sprite = null;
                mQualityBorderImage.gameObject.SetActive(false);
            }
            if (mLockedOverlay != null)
            {
                mLockedOverlay.SetActive(false);
            }
        }
    }
}
