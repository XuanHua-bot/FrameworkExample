using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 项目统一按钮组件
    /// 统一交互状态机 (Normal, Pressed, Hover, Busy, Cooldown, Disabled)
    /// 点击防抖、请求锁定(Busy)、CD冷却倒计时、音效解耦请求与对象池重置
    /// 既可作为独立增强组件与原生 Button 协作，亦可独立捕获指针事件
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("References")]
        [Tooltip("关联的原生 Button 组件，若未配置则在 Awake 时自动获取")]
        [SerializeField] private Button mButton;

        [Tooltip("按钮文本控件（可选）")]
        [SerializeField] private TMP_Text mLabel;

        [Tooltip("按钮图标控件（可选）")]
        [SerializeField] private Image mIcon;

        [Header("Audio")]
        [Tooltip("按钮点击音效，可空表示无音效")]
        [SerializeField] private AudioCueKey mClickSound;

        [Header("Debounce & Cooldown")]
        [Tooltip("点击防抖时间（秒），防止连点，默认 0.15s，0 表示不防抖")]
        [SerializeField] private float mDebounceSeconds = 0.15f;

        [Header("Visual Feedback (Optional)")]
        [Tooltip("是否启用按压视觉缩放效果")]
        [SerializeField] private bool mEnableScaleEffect = true;

        [Tooltip("按压时的目标变换，留空则为当前 transform")]
        [SerializeField] private Transform mTargetTransform;

        [Tooltip("按压时的缩放比例")]
        [SerializeField] private Vector3 mPressedScale = new Vector3(0.95f, 0.95f, 1f);

        [Tooltip("松开恢复的正常缩放比例")]
        [SerializeField] private Vector3 mNormalScale = Vector3.one;

        [Tooltip("缩放动画过渡时间（秒）")]
        [SerializeField] private float mScaleDuration = 0.08f;

        // 内部状态
        private bool mIsBusy;
        private bool mInteractableBeforeBusy = true;
        private bool mIsPointerDown;
        private float mLastClickTime = -10f;
        private Coroutine mCooldownCoroutine;
        private Coroutine mScaleCoroutine;

        /// <summary>
        /// 当前按钮是否可交互
        /// </summary>
        public bool Interactable
        {
            get => mButton != null ? mButton.interactable : (State != UIButtonState.Disabled);
            set => SetInteractable(value);
        }

        /// <summary>
        /// 当前按钮是否处于 忙碌/等待业务响应状态
        /// </summary>
        public bool IsBusy => mIsBusy;

        /// <summary>
        /// 当前按钮所处的统一交互状态
        /// </summary>
        public UIButtonState State { get; private set; } = UIButtonState.Normal;

        /// <summary>
        /// 按钮有效点击事件（防抖通过且处于 Normal 状态时触发），页面 Controller 应订阅此事件
        /// </summary>
        public event Action Clicked;

        /// <summary>
        /// 按钮请求播放音效事件，由外部统一音频服务或页面订阅，组件不直接耦合 IAudioService
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        /// <summary>
        /// 按钮状态变更事件 (newState, oldState)
        /// </summary>
        public event Action<UIButtonState, UIButtonState> StateChanged;

        /// <summary>
        /// 冷却进度或倒计时更新事件 (remainingSeconds, totalDuration)
        /// </summary>
        public event Action<float, float> CooldownProgress;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (mButton == null)
            {
                mButton = GetComponent<Button>();
            }

            if (mTargetTransform == null)
            {
                mTargetTransform = transform;
            }

            mNormalScale = mTargetTransform.localScale;

            bool isInitiallyInteractable = mButton != null ? mButton.interactable : true;
            State = isInitiallyInteractable ? UIButtonState.Normal : UIButtonState.Disabled;
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (mButton != null)
            {
                mButton.onClick.RemoveListener(HandleUnityClick);
                mButton.onClick.AddListener(HandleUnityClick);
            }
        }

        private void OnDisable()
        {
            if (mButton != null)
            {
                mButton.onClick.RemoveListener(HandleUnityClick);
            }

            StopAllRunningCoroutines();
            ResetScaleImmediate();
        }

        /// <summary>
        /// 设置按钮文本
        /// </summary>
        public void SetText(string text)
        {
            if (mLabel != null)
            {
                mLabel.text = text;
            }
        }

        /// <summary>
        /// 设置按钮图标
        /// </summary>
        public void SetIcon(Sprite icon)
        {
            if (mIcon != null)
            {
                mIcon.sprite = icon;
                mIcon.gameObject.SetActive(icon != null);
            }
        }

        /// <summary>
        /// 配置点击音效
        /// </summary>
        public void SetClickSound(AudioCueKey soundKey)
        {
            mClickSound = soundKey;
        }

        /// <summary>
        /// 设置按钮的交互可用性
        /// </summary>
        public void SetInteractable(bool value)
        {
            if (mIsBusy)
            {
                mInteractableBeforeBusy = value;
                return;
            }

            if (mButton != null)
            {
                mButton.interactable = value;
            }

            SetState(value ? UIButtonState.Normal : UIButtonState.Disabled);
        }

        /// <summary>
        /// 尝试进入 Busy 状态
        /// 异步网络请求或重度操作开始前调用，成功后按钮会立即禁止重复点击
        /// </summary>
        public bool TryBeginBusy()
        {
            if (mIsBusy || State != UIButtonState.Normal)
            {
                return false;
            }

            mInteractableBeforeBusy = mButton != null ? mButton.interactable : true;
            mIsBusy = true;

            if (mButton != null)
            {
                mButton.interactable = false;
            }

            SetState(UIButtonState.Busy);
            return true;
        }

        /// <summary>
        /// 结束 Busy 状态，请求成功、失败、超时或取消时调用
        /// </summary>
        public void EndBusy()
        {
            if (!mIsBusy) return;

            mIsBusy = false;

            if (mButton != null)
            {
                mButton.interactable = mInteractableBeforeBusy;
            }

            SetState(mInteractableBeforeBusy ? UIButtonState.Normal : UIButtonState.Disabled);
        }

        /// <summary>
        /// 开启技能或交互冷却
        /// </summary>
        /// <param name="durationSeconds">冷却总时长（秒）</param>
        public void BeginCooldown(float durationSeconds)
        {
            if (State != UIButtonState.Normal && State != UIButtonState.Cooldown)
            {
                return;
            }

            if (mCooldownCoroutine != null)
            {
                StopCoroutine(mCooldownCoroutine);
            }

            if (mButton != null)
            {
                mButton.interactable = false;
            }

            SetState(UIButtonState.Cooldown);
            mCooldownCoroutine = StartCoroutine(CooldownRoutine(durationSeconds));
        }

        /// <summary>
        /// 清理冷却状态并恢复可点击
        /// </summary>
        public void ClearCooldown()
        {
            if (State != UIButtonState.Cooldown) return;

            if (mCooldownCoroutine != null)
            {
                StopCoroutine(mCooldownCoroutine);
                mCooldownCoroutine = null;
            }

            if (mButton != null)
            {
                mButton.interactable = true;
            }

            SetState(UIButtonState.Normal);
            CooldownProgress?.Invoke(0f, 0f);
        }

        private IEnumerator CooldownRoutine(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float remaining = Mathf.Max(0f, duration - elapsed);
                CooldownProgress?.Invoke(remaining, duration);
                yield return null;
            }

            mCooldownCoroutine = null;
            if (mButton != null)
            {
                mButton.interactable = true;
            }

            SetState(UIButtonState.Normal);
            CooldownProgress?.Invoke(0f, duration);
        }

        /// <summary>
        /// 原生 Button 的 onClick 回调或单独点击事件分发
        /// </summary>
        private void HandleUnityClick()
        {
            TriggerClick();
        }

        /// <summary>
        /// 独立 IPointerClickHandler 支持（当挂载物没有原生 Button 组件时亦可直接触发）
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            // 若存在原生 Button，则点击已由 Button.onClick 驱动触发 HandleUnityClick，避免一次点击触发两次
            if (mButton == null)
            {
                TriggerClick();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (State != UIButtonState.Normal) return;

            mIsPointerDown = true;
            PlayScaleAnimation(mPressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            mIsPointerDown = false;
            PlayScaleAnimation(mNormalScale);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (mIsPointerDown && State == UIButtonState.Normal)
            {
                PlayScaleAnimation(mPressedScale);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (mIsPointerDown)
            {
                PlayScaleAnimation(mNormalScale);
            }
        }

        private void TriggerClick()
        {
            if (State != UIButtonState.Normal)
            {
                return;
            }

            if (mButton != null && !mButton.interactable)
            {
                return;
            }

            // 点击防抖判断
            float now = Time.unscaledTime;
            if (mDebounceSeconds > 0f && (now - mLastClickTime) < mDebounceSeconds)
            {
                return;
            }
            mLastClickTime = now;

            // 请求播放音效
            if (mClickSound.IsValid)
            {
                SoundRequested?.Invoke(mClickSound);
            }

            Clicked?.Invoke();
        }

        private void SetState(UIButtonState newState)
        {
            if (State == newState) return;

            var oldState = State;
            State = newState;
            StateChanged?.Invoke(newState, oldState);

            if (State != UIButtonState.Normal)
            {
                ResetScaleImmediate();
            }
        }

        private void PlayScaleAnimation(Vector3 targetScale)
        {
            if (!mEnableScaleEffect || mTargetTransform == null || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (mScaleCoroutine != null)
            {
                StopCoroutine(mScaleCoroutine);
            }

            mScaleCoroutine = StartCoroutine(AnimateScaleRoutine(targetScale));
        }

        private IEnumerator AnimateScaleRoutine(Vector3 targetScale)
        {
            Vector3 startScale = mTargetTransform.localScale;
            float elapsed = 0f;

            while (elapsed < mScaleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / mScaleDuration);
                mTargetTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            mTargetTransform.localScale = targetScale;
            mScaleCoroutine = null;
        }

        private void ResetScaleImmediate()
        {
            if (mScaleCoroutine != null)
            {
                StopCoroutine(mScaleCoroutine);
                mScaleCoroutine = null;
            }

            if (mTargetTransform != null)
            {
                mTargetTransform.localScale = mNormalScale;
            }
        }

        private void StopAllRunningCoroutines()
        {
            if (mCooldownCoroutine != null)
            {
                StopCoroutine(mCooldownCoroutine);
                mCooldownCoroutine = null;
            }

            if (mScaleCoroutine != null)
            {
                StopCoroutine(mScaleCoroutine);
                mScaleCoroutine = null;
            }
        }

        /// <summary>
        /// 对象池回收或页面复用时重置
        /// </summary>
        public virtual void ResetForPool()
        {
            StopAllRunningCoroutines();
            ResetScaleImmediate();

            mIsBusy = false;
            mInteractableBeforeBusy = true;
            mIsPointerDown = false;
            mLastClickTime = -10f;

            Clicked = null;
            SoundRequested = null;
            StateChanged = null;
            CooldownProgress = null;

            if (mButton != null)
            {
                mButton.interactable = true;
            }

            State = UIButtonState.Normal;
        }

        private void OnDestroy()
        {
            StopAllRunningCoroutines();
            Clicked = null;
            SoundRequested = null;
            StateChanged = null;
            CooldownProgress = null;
        }
    }
}
