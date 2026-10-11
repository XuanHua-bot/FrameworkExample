using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// Tab 选项卡视觉模式
    /// </summary>
    public enum UITabVisualMode
    {
        /// <summary>
        /// 游戏对象激活/隐藏切换（例如针对具有两个独立子节点的结构，如 SelectedState / UnselectedState）
        /// </summary>
        GameObjectToggle,

        /// <summary>
        /// 颜色染色过渡（对背景和文本进行颜色切换）
        /// </summary>
        ColorTint,

        /// <summary>
        /// Sprite 纹理切换
        /// </summary>
        SpriteSwap,

        /// <summary>
        /// 自定义（通过监听 OnSelectedChanged 事件在外部或派生类中控制表现）
        /// </summary>
        Custom
    }

    /// <summary>
    /// 单个 Tab 选项卡组件
    /// 纯表现与通用交互，不直接持有业务数据与 Model
    /// </summary>
    [DisallowMultipleComponent]
    public class UITabItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Identities")]
        [Tooltip("Tab 的唯一标识符（字符串形式），用于业务逻辑查找与区分")]
        [SerializeField] private string mTabKey = string.Empty;

        [Tooltip("Tab 序号索引，未指定或默认可由 UITabGroup 注册时动态分配")]
        [SerializeField] private int mIndex = -1;

        [Header("State")]
        [SerializeField] private bool mIsSelected;
        [SerializeField] private bool mInteractable = true;

        [Header("Audio")]
        [Tooltip("被选中或点击时请求的音频 CueKey")]
        [SerializeField] private AudioCueKey mSelectSound;

        [Header("Visual Config")]
        [SerializeField] private UITabVisualMode mVisualMode = UITabVisualMode.GameObjectToggle;

        [Tooltip("选中状态下激活的物体（VisualMode = GameObjectToggle 时生效）")]
        [SerializeField] private GameObject mSelectedObject;

        [Tooltip("未选中状态下激活的物体（VisualMode = GameObjectToggle 时生效）")]
        [SerializeField] private GameObject mUnselectedObject;

        [Tooltip("背景图片引用（用于 ColorTint 或 SpriteSwap 模式）")]
        [SerializeField] private Image mTargetGraphic;

        [Tooltip("SpriteSwap 模式下的精灵配置")]
        [SerializeField] private Sprite mNormalSprite;
        [SerializeField] private Sprite mSelectedSprite;

        [Tooltip("ColorTint 模式下的颜色配置")]
        [SerializeField] private Color mNormalColor = Color.white;
        [SerializeField] private Color mSelectedColor = Color.white;
        [SerializeField] private Color mDisabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);

        [Header("Label & Icon (Optional)")]
        [SerializeField] private TMP_Text mLabel;
        [SerializeField] private Image mIcon;

        // 私有运行时数据
        private UITabGroup mOwnerGroup;

        /// <summary>
        /// 所属的 TabGroup
        /// </summary>
        public UITabGroup OwnerGroup => mOwnerGroup;

        /// <summary>
        /// Tab 唯一键标识
        /// </summary>
        public string TabKey
        {
            get => mTabKey;
            set => mTabKey = value;
        }

        /// <summary>
        /// Tab 序号索引
        /// </summary>
        public int Index
        {
            get => mIndex;
            set => mIndex = value;
        }

        /// <summary>
        /// 当前是否处于选中状态
        /// </summary>
        public bool IsSelected => mIsSelected;

        /// <summary>
        /// 当前是否可交互
        /// </summary>
        public bool Interactable
        {
            get => mInteractable;
            set
            {
                if (mInteractable != value)
                {
                    mInteractable = value;
                    ApplyVisualState();
                }
            }
        }

        /// <summary>
        /// 点击或选中请求播放音效事件（解耦 IAudioService）
        /// </summary>
        public event Action<AudioCueKey> SoundRequested;

        /// <summary>
        /// 选中状态变更事件 (isSelected, isUserAction)
        /// </summary>
        public event Action<bool, bool> SelectedChanged;

        /// <summary>
        /// 点击回调 (item)
        /// </summary>
        public event Action<UITabItem> Clicked;

        public bool HasSelectSound => mSelectSound.IsValid;

        private void Awake()
        {
            if (mTargetGraphic == null)
            {
                mTargetGraphic = GetComponent<Image>();
            }
        }

        private void Start()
        {
            ApplyVisualState();
        }

        /// <summary>
        /// 绑定所属的 UITabGroup
        /// </summary>
        public void BindGroup(UITabGroup group)
        {
            mOwnerGroup = group;
        }

        /// <summary>
        /// 解绑所属 UITabGroup
        /// </summary>
        public void UnbindGroup()
        {
            mOwnerGroup = null;
        }

        /// <summary>
        /// 设置标签文本内容
        /// </summary>
        public void SetText(string text)
        {
            if (mLabel != null)
            {
                mLabel.text = text;
            }
        }

        /// <summary>
        /// 设置图标
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
        /// 设置音频 Key
        /// </summary>
        public void SetSelectSound(AudioCueKey soundKey)
        {
            mSelectSound = soundKey;
        }

        /// <summary>
        /// 设置选中状态
        /// </summary>
        /// <param name="selected">是否选中</param>
        /// <param name="isUserAction">是否来自用户交互</param>
        /// <param name="notifyGroup">是否通知绑定的 TabGroup 同步</param>
        public void SetSelected(bool selected, bool isUserAction = false, bool notifyGroup = true)
        {
            if (mIsSelected == selected)
            {
                return;
            }

            mIsSelected = selected;
            ApplyVisualState();

            SelectedChanged?.Invoke(mIsSelected, isUserAction);

            if (notifyGroup && mOwnerGroup != null && mIsSelected)
            {
                mOwnerGroup.OnItemSelectRequested(this, isUserAction);
            }
        }

        /// <summary>
        /// Unity PointerClick 事件处理
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!mInteractable || !isActiveAndEnabled)
            {
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            Clicked?.Invoke(this);

            if (mOwnerGroup != null)
            {
                mOwnerGroup.OnItemSelectRequested(this, isUserAction: true);
            }
            else
            {
                SetSelected(true, isUserAction: true, notifyGroup: false);
            }

            if (mSelectSound.IsValid)
            {
                SoundRequested?.Invoke(mSelectSound);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // 为后续 Hover 状态或 Tooltip 预留接口
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 为后续 Hover 状态或 Tooltip 预留接口
        }

        /// <summary>
        /// 刷新视觉表现
        /// </summary>
        public virtual void ApplyVisualState()
        {
            switch (mVisualMode)
            {
                case UITabVisualMode.GameObjectToggle:
                    if (mSelectedObject != null)
                    {
                        mSelectedObject.SetActive(mIsSelected);
                    }
                    if (mUnselectedObject != null)
                    {
                        mUnselectedObject.SetActive(!mIsSelected);
                    }
                    break;

                case UITabVisualMode.SpriteSwap:
                    if (mTargetGraphic != null)
                    {
                        mTargetGraphic.sprite = mIsSelected ? mSelectedSprite : mNormalSprite;
                    }
                    break;

                case UITabVisualMode.ColorTint:
                    if (mTargetGraphic != null)
                    {
                        if (!mInteractable)
                        {
                            mTargetGraphic.color = mDisabledColor;
                        }
                        else
                        {
                            mTargetGraphic.color = mIsSelected ? mSelectedColor : mNormalColor;
                        }
                    }
                    break;

                case UITabVisualMode.Custom:
                    break;
            }
        }

        /// <summary>
        /// 重置状态（供对象池回收或页面复用时清理）
        /// </summary>
        public virtual void ResetForPool()
        {
            mIsSelected = false;
            mInteractable = true;
            mOwnerGroup = null;
            mIndex = -1;
            mTabKey = string.Empty;

            SelectedChanged = null;
            Clicked = null;
            SoundRequested = null;

            ApplyVisualState();
        }

        private void OnDestroy()
        {
            SelectedChanged = null;
            Clicked = null;
            SoundRequested = null;
            mOwnerGroup = null;
        }
    }
}
