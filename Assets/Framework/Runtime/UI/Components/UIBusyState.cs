using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// UI 忙碌遮罩状态组件
    /// 用于整个界面、指定区域或弹窗的异步等待遮罩
    /// 支持拦截射线输入、超时自动解开保护、挂载指示器联动及提示文案配置
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIBusyState : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private CanvasGroup mCanvasGroup;

        [Tooltip("可选的加载转圈指示器")]
        [SerializeField] private UILoadingIndicator mLoadingIndicator;

        [Tooltip("提示文字控件（可选）")]
        [SerializeField] private TMP_Text mTipText;

        [Header("Block Input")]
        [Tooltip("处于 Busy 状态时是否阻止用户点击穿透（设置 blocksRaycasts = true）")]
        [SerializeField] private bool mBlockRaycasts = true;

        [Header("Timeout Safety")]
        [Tooltip("超时安全强制退出时间（秒），防止服务器未响应导致 UI 永久卡死。默认 15s，<=0 表示无超时")]
        [SerializeField] private float mDefaultTimeout = 15f;

        // 运行时状态
        private bool mIsBusy;
        private int mBusyCount; // 嵌套计数器，支持并发多个异步请求
        private Coroutine mTimeoutCoroutine;

        public bool IsBusy => mIsBusy;

        /// <summary>
        /// 当忙碌状态开始或结束时触发 (isBusy)
        /// </summary>
        public event Action<bool> BusyStateChanged;

        /// <summary>
        /// 当触发超时强制解除时通知，业务层可据此上报错误或展示断网提示
        /// </summary>
        public event Action TimeoutTriggered;

        private void Awake()
        {
            if (mCanvasGroup == null)
            {
                mCanvasGroup = GetComponent<CanvasGroup>();
            }

            // 初始时默认不显示忙碌
            ApplyVisualState(false);
        }

        private void OnDisable()
        {
            if (mTimeoutCoroutine != null)
            {
                StopCoroutine(mTimeoutCoroutine);
                mTimeoutCoroutine = null;
            }
        }

        /// <summary>
        /// 开启忙碌状态
        /// </summary>
        /// <param name="tip">提示文案（如：正在连接服务器...）</param>
        /// <param name="timeoutSeconds">自定义超时时间，若小于等于 0 则使用默认配置</param>
        public void BeginBusy(string tip = null, float timeoutSeconds = -1f)
        {
            mBusyCount++;

            if (!string.IsNullOrEmpty(tip))
            {
                SetTip(tip);
            }

            if (!mIsBusy)
            {
                mIsBusy = true;
                ApplyVisualState(true);
                BusyStateChanged?.Invoke(true);
            }

            // 重设或延长超时定时器
            float timeout = timeoutSeconds > 0f ? timeoutSeconds : mDefaultTimeout;
            if (timeout > 0f && isActiveAndEnabled)
            {
                if (mTimeoutCoroutine != null)
                {
                    StopCoroutine(mTimeoutCoroutine);
                }
                mTimeoutCoroutine = StartCoroutine(TimeoutRoutine(timeout));
            }
        }

        /// <summary>
        /// 页面/遮罩在 BeginBusy 之后才被激活时（UI 服务先调用 OnOpen、随后才 SetActive），
        /// 在此补一次表现状态，避免协程因宿主未激活而启动失败。
        /// </summary>
        private void OnEnable()
        {
            if (!mIsBusy)
            {
                return;
            }

            ApplyVisualState(true);

            if (mDefaultTimeout > 0f && mTimeoutCoroutine == null)
            {
                mTimeoutCoroutine = StartCoroutine(TimeoutRoutine(mDefaultTimeout));
            }
        }

        /// <summary>
        /// 结束忙碌状态（计数减一）
        /// </summary>
        public void EndBusy()
        {
            if (mBusyCount > 0)
            {
                mBusyCount--;
            }

            if (mBusyCount == 0 && mIsBusy)
            {
                ForceEndBusyInternal();
            }
        }

        /// <summary>
        /// 强制清除所有忙碌状态并重置计数（如换页、重连或网络失败时调用）
        /// </summary>
        public void ForceEndBusy()
        {
            mBusyCount = 0;
            if (mIsBusy)
            {
                ForceEndBusyInternal();
            }
        }

        private void ForceEndBusyInternal()
        {
            mIsBusy = false;
            mBusyCount = 0;

            if (mTimeoutCoroutine != null)
            {
                StopCoroutine(mTimeoutCoroutine);
                mTimeoutCoroutine = null;
            }

            ApplyVisualState(false);
            BusyStateChanged?.Invoke(false);
        }

        public void SetTip(string tip)
        {
            if (mTipText != null)
            {
                mTipText.text = tip;
            }

            if (mLoadingIndicator != null)
            {
                mLoadingIndicator.SetTip(tip);
            }
        }

        private void ApplyVisualState(bool busy)
        {
            if (mCanvasGroup != null)
            {
                mCanvasGroup.alpha = busy ? 1f : 0f;
                mCanvasGroup.blocksRaycasts = busy && mBlockRaycasts;
                mCanvasGroup.interactable = busy;
            }

            if (mLoadingIndicator != null)
            {
                if (busy)
                {
                    mLoadingIndicator.gameObject.SetActive(true);
                    mLoadingIndicator.StartSpin();
                }
                else
                {
                    mLoadingIndicator.StopSpin();
                    mLoadingIndicator.gameObject.SetActive(false);
                }
            }
        }

        private IEnumerator TimeoutRoutine(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            mTimeoutCoroutine = null;

            if (mIsBusy)
            {
                Debug.LogWarning($"[UIBusyState] Busy state timed out after {seconds}s! Force unlocking UI.");
                ForceEndBusy();
                TimeoutTriggered?.Invoke();
            }
        }

        /// <summary>
        /// 对象池复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            ForceEndBusy();
            BusyStateChanged = null;
            TimeoutTriggered = null;

            if (mLoadingIndicator != null)
            {
                mLoadingIndicator.ResetForPool();
            }

            if (mTipText != null)
            {
                mTipText.text = string.Empty;
            }
        }

        private void OnDestroy()
        {
            ForceEndBusy();
            BusyStateChanged = null;
            TimeoutTriggered = null;
        }
    }
}
