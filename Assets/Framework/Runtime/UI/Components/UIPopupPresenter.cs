using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 弹窗展示参数选项
    /// </summary>
    public struct UIPopupOptions
    {
        public string Title;
        public string Message;
        public string ConfirmText;
        public string CancelText;
        public Action OnConfirm;
        public Action OnCancel;
        public bool ShowCancelButton;
        public bool CloseOnMaskClick;

        public static UIPopupOptions CreateAlert(string message, string title = "提示", Action onConfirm = null)
        {
            return new UIPopupOptions
            {
                Title = title,
                Message = message,
                ConfirmText = "确定",
                CancelText = string.Empty,
                OnConfirm = onConfirm,
                OnCancel = null,
                ShowCancelButton = false,
                CloseOnMaskClick = false
            };
        }

        public static UIPopupOptions CreateConfirm(string message, string title = "确认", Action onConfirm = null, Action onCancel = null)
        {
            return new UIPopupOptions
            {
                Title = title,
                Message = message,
                ConfirmText = "确定",
                CancelText = "取消",
                OnConfirm = onConfirm,
                OnCancel = onCancel,
                ShowCancelButton = true,
                CloseOnMaskClick = false
            };
        }
    }

    /// <summary>
    /// 通用通用弹窗展示器组件（P0 表现组件）
    /// 可挂载于 Concave 的 Popup 预制体（如 Popup Base / Popup - Size Fitter）
    /// 负责标题、内容、确定/取消按钮及遮罩关闭逻辑，提供标准弹窗展现行为
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPopupPresenter : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text mTitleText;
        [SerializeField] private TMP_Text mMessageText;

        [Header("Buttons")]
        [SerializeField] private UIButton mConfirmButton;
        [SerializeField] private UIButton mCancelButton;
        [SerializeField] private UIButton mCloseButton;

        [Header("Mask (Optional)")]
        [Tooltip("黑色背景遮罩（可点击关闭）")]
        [SerializeField] private Button mMaskButton;

        [Header("Audio")]
        [SerializeField] private AudioCueKey mOpenSound;
        [SerializeField] private AudioCueKey mCloseSound;

        // 运行时状态与回调
        private UIPopupOptions mOptions;
        private bool mIsOpen;

        public bool IsOpen => mIsOpen;

        /// <summary>
        /// 弹窗开启事件
        /// </summary>
        public event Action Opened;

        /// <summary>
        /// 弹窗关闭事件 (isConfirmed)
        /// </summary>
        public event Action<bool> Closed;

        /// <summary>
        /// 音效请求事件（解耦 IAudioService）
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        private void Awake()
        {
            BindButtonEvents();
        }

        private void BindButtonEvents()
        {
            if (mConfirmButton != null)
            {
                mConfirmButton.Clicked += HandleConfirmClicked;
            }

            if (mCancelButton != null)
            {
                mCancelButton.Clicked += HandleCancelClicked;
            }

            if (mCloseButton != null)
            {
                mCloseButton.Clicked += HandleCancelClicked;
            }

            if (mMaskButton != null)
            {
                mMaskButton.onClick.AddListener(HandleMaskClicked);
            }
        }

        /// <summary>
        /// 打开并配置弹窗内容
        /// </summary>
        public void Show(UIPopupOptions options)
        {
            mOptions = options;
            mIsOpen = true;

            if (mTitleText != null)
            {
                mTitleText.text = options.Title ?? string.Empty;
            }

            if (mMessageText != null)
            {
                mMessageText.text = options.Message ?? string.Empty;
            }

            if (mConfirmButton != null)
            {
                if (!string.IsNullOrEmpty(options.ConfirmText))
                {
                    mConfirmButton.SetText(options.ConfirmText);
                }
                mConfirmButton.gameObject.SetActive(true);
            }

            if (mCancelButton != null)
            {
                if (!string.IsNullOrEmpty(options.CancelText))
                {
                    mCancelButton.SetText(options.CancelText);
                }
                mCancelButton.gameObject.SetActive(options.ShowCancelButton);
            }

            gameObject.SetActive(true);

            if (mOpenSound.IsValid)
            {
                SoundRequested?.Invoke(mOpenSound);
            }

            Opened?.Invoke();
        }

        public void Close(bool isConfirmed)
        {
            if (!mIsOpen) return;

            mIsOpen = false;
            gameObject.SetActive(false);

            if (mCloseSound.IsValid)
            {
                SoundRequested?.Invoke(mCloseSound);
            }

            Closed?.Invoke(isConfirmed);
        }

        private void HandleConfirmClicked()
        {
            var callback = mOptions.OnConfirm;
            Close(isConfirmed: true);
            callback?.Invoke();
        }

        private void HandleCancelClicked()
        {
            var callback = mOptions.OnCancel;
            Close(isConfirmed: false);
            callback?.Invoke();
        }

        private void HandleMaskClicked()
        {
            if (mOptions.CloseOnMaskClick)
            {
                HandleCancelClicked();
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            mIsOpen = false;
            mOptions = default;
            Opened = null;
            Closed = null;
            SoundRequested = null;

            if (mConfirmButton != null) mConfirmButton.ResetForPool();
            if (mCancelButton != null) mCancelButton.ResetForPool();
            if (mCloseButton != null) mCloseButton.ResetForPool();

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (mConfirmButton != null) mConfirmButton.Clicked -= HandleConfirmClicked;
            if (mCancelButton != null) mCancelButton.Clicked -= HandleCancelClicked;
            if (mCloseButton != null) mCloseButton.Clicked -= HandleCancelClicked;
            if (mMaskButton != null) mMaskButton.onClick.RemoveListener(HandleMaskClicked);

            Opened = null;
            Closed = null;
            SoundRequested = null;
        }
    }
}
