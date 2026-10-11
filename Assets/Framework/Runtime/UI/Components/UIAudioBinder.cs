using UnityEngine;

namespace GameFramework
{
    /// <summary>把页面内组件请求的音效交给项目音频服务，包含虚拟列表滚动生成的条目。</summary>
    [DisallowMultipleComponent]
    public sealed class UIAudioBinder : MonoBehaviour
    {
        [SerializeField] private string mGroup;
        [SerializeField] private AudioCueKey mPageOpenSound;
        [SerializeField] private AudioCueKey mPageCloseSound;

        private IAudioService mAudio;
        private bool mBound;
        private bool mReady;
        private bool mOpenPending;
        private bool mClosed;

        public string Group { get => mGroup; set => mGroup = value; }
        public AudioCueKey PageOpenSound { get => mPageOpenSound; set => mPageOpenSound = value; }
        public AudioCueKey PageCloseSound { get => mPageCloseSound; set => mPageCloseSound = value; }

        public void Bind(IAudioService audio)
        {
            if (mBound || audio == null || string.IsNullOrWhiteSpace(mGroup)) return;
            mBound = true;
            mAudio = audio;

            foreach (var button in GetComponentsInChildren<UIButton>(true)) button.SoundRequested += Play;
            foreach (var item in GetComponentsInChildren<UIListItem>(true)) BindItem(item);
            foreach (var list in GetComponentsInChildren<UISimpleList>(true)) list.ItemBound += BindItem;
            foreach (var list in GetComponentsInChildren<UIVirtualList>(true)) list.ItemBound += BindItem;
            foreach (var group in GetComponentsInChildren<UITabGroup>(true)) group.SoundRequested += Play;
            foreach (var tab in GetComponentsInChildren<UITabItem>(true)) tab.SoundRequested += Play;
            foreach (var input in GetComponentsInChildren<UIInputField>(true)) input.SoundRequested += Play;
            foreach (var slider in GetComponentsInChildren<UISlider>(true)) slider.SoundRequested += Play;
            foreach (var drag in GetComponentsInChildren<UIDragHandle>(true)) drag.SoundRequested += Play;
            foreach (var drop in GetComponentsInChildren<UIDropTarget>(true)) drop.SoundRequested += Play;
            foreach (var popup in GetComponentsInChildren<UIPopupPresenter>(true)) popup.SoundRequested += Play;
            foreach (var window in GetComponentsInChildren<UIWindowAnimation>(true)) window.SoundRequested += Play;
            foreach (var message in GetComponentsInChildren<UIMessagePresenter>(true)) message.SoundRequested += Play;

            mAudio.LoadGroupAsync(mGroup, loaded =>
            {
                if (this == null || !mBound) return;
                mReady = loaded;
                if (loaded && mOpenPending && !mClosed) { mOpenPending = false; Play(mPageOpenSound); }
            });
        }

        private void BindItem(UIListItem item)
        {
            if (item == null) return;
            // UIListItem.ResetForPool 清空事件；每次重绑时恢复订阅。
            item.SoundRequested -= Play;
            item.SoundRequested += Play;
            var drag = item.GetComponent<UIDragHandle>();
            if (drag != null)
            {
                drag.SoundRequested -= Play;
                drag.SoundRequested += Play;
            }
        }

        public void PlayShown()
        {
            mClosed = false;
            if (mReady) Play(mPageOpenSound);
            else mOpenPending = true;
        }

        public void PlayHidden()
        {
            mClosed = true;
            mOpenPending = false;
            Play(mPageCloseSound);
        }

        private void Play(AudioCueKey key)
        {
            if (mReady && key.IsValid) mAudio.Play(key);
        }

        private void OnDestroy()
        {
            if (!mBound) return;
            mBound = false;
            foreach (var list in GetComponentsInChildren<UISimpleList>(true)) list.ItemBound -= BindItem;
            foreach (var list in GetComponentsInChildren<UIVirtualList>(true)) list.ItemBound -= BindItem;
            mAudio.UnloadGroup(mGroup);
            mAudio = null;
        }
    }
}
