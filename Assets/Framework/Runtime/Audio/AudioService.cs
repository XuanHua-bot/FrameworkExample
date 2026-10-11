using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace GameFramework
{
    public class AudioService:IAudioService,IDeinit
    {
        private sealed class LoadedGroup
        {
            public AssetHandle<AudioResourceGroup> Handle;//存活到所有播放实例结束

            public readonly Dictionary<string, AudioResourceGroup.Entry>
                Cues = new Dictionary<string, AudioResourceGroup.Entry>();
            //多个页面可以共享 同一组 按使用者配对Load/Unload
            public int Users;
            public int Playing;

        }
        private sealed class  PendingGroup
        {
            public readonly List<Action<bool>> Listeners = new List<Action<bool>>();
            public int Users;
        }

        private readonly IAssetLoader mLoader;
        private readonly Dictionary<string, LoadedGroup> mGroups = new Dictionary<string, LoadedGroup>();
        private readonly Dictionary<string, PendingGroup> mPending = new Dictionary<string, PendingGroup>();
        private AssetHandle<AudioMixer> mMixerHandle;
        private AudioDriver mDriver;
        private AudioCueKey mMusicKey;
        private bool mHasMusic;
        private bool mDisposed;

        public AudioService(IAssetLoader loader)
        {
            mLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            mMixerHandle = loader.Load<AudioMixer>("Audio/MasterMixer");
            if (mMixerHandle==null)throw new InvalidOperationException("缺少 Audio/MasterMixer");
            try
            {
                mDriver = AudioDriver.Create(mMixerHandle.Asset);
            }
            catch
            {
                mMixerHandle.Release();
                throw;
            }
            
        }
        
        public void LoadGroupAsync(string group, Action<bool> completed)
        {
            if (string.IsNullOrWhiteSpace(group)||mDisposed)
            {
                completed?.Invoke(false);
                return;
            }

            if (mGroups.TryGetValue(group,out var loaded))
            {
                loaded.Users++;
                completed?.Invoke(true);
                return;
            }
            //合并同一组的并发加载, 避免重复请求 和重复handle
            if (mPending.TryGetValue(group,out var pending))
            {
                pending.Users++;
                pending.Listeners.Add(completed);
                return;
            }

            pending = new PendingGroup { Users = 1 };
            pending.Listeners.Add(completed);
            mPending.Add(group, pending);
            mLoader.LoadAsync<AudioResourceGroup>(group,handle=>CompleteLoad(group,handle),reason=>FailLoad(group,reason));
        }

        private void CompleteLoad(string group, AssetHandle<AudioResourceGroup> handle)
        {
            if (mDisposed||!mPending.TryGetValue(group,out var pending)||pending.Users ==0)
            {
                handle.Release();
                NotifyLoaded(group, false);
                return;
            }
            //pending.Usere 是加载期间的使用者数量, 移置到已加载组
            var loaded = new LoadedGroup { Handle = handle, Users = pending.Users };
            var entries = handle.Asset.Entries;
            if (entries!=null)
            {
                foreach (var cue in entries)
                {
                    if (cue ==null||cue.Clip==null||string.IsNullOrWhiteSpace(cue.Id))continue;
                    if (loaded.Cues.ContainsKey(cue.Id))
                    {
                        Debug.LogWarning($"组内重复的音频事件名 {cue.Id}"+" 组名: "+group);
                        continue;
                    }
                    loaded.Cues.Add(cue.Id,cue);
                }
            }
            mGroups.Add(group, loaded);
            NotifyLoaded(group, true);
        }

        void FailLoad(string group,string reason)
        {
            if (!mDisposed) Debug.LogWarning("音频组加载失败"+group+"," + reason);
            NotifyLoaded(group, false);
        }

        private void NotifyLoaded(string group, bool success)
        {
            if (!mPending.TryGetValue(group, out var pending)) return;
            mPending.Remove(group);
            foreach (var listener in pending.Listeners) listener?.Invoke(success);
        }

        public void UnloadGroup(string group)
        {
            if (mPending.TryGetValue(group, out var pending) && pending.Users > 0) pending.Users--;
            if (!mGroups.TryGetValue(group, out var loaded)) return;
            if (loaded.Users > 0) loaded.Users--;
            ReleaseIfUnused(group, loaded);

        }

        public int Play(AudioCueKey key)
        {
            if (!TryGetCue(key, out var group, out var cue)) return -1;
            group.Playing++;
            var id = mDriver.PlayOneShot(cue.Clip, group.Handle.Asset.OutputGroup,
                cue.Volume, () => Finished(key.Group, group));
            if (id < 0) Finished(key.Group, group);
            return id;
        }

         public void Stop(int playbackId) => mDriver?.Stop(playbackId);

        public void PlayMusic(AudioCueKey key, float fadeSeconds = 0.25f)
        {
            if (mHasMusic && mMusicKey.Group == key.Group &&
                mMusicKey.Cue == key.Cue) return;
            if (!TryGetCue(key, out var group, out var cue)) return;
            group.Playing++;
            mDriver.PlayMusic(cue.Clip, cue.Volume, Mathf.Max(0f, fadeSeconds),
                () => Finished(key.Group, group));
            mMusicKey = key;
            mHasMusic = true;
        }

        public void StopMusic(float fadeSeconds = 0.2f)
        {
            mHasMusic = false;
            mDriver?.StopMusic(Mathf.Max(0f, fadeSeconds));
        }

        public void PauseAll() => mDriver?.PauseAll();
        public void ResumeAll() => mDriver?.ResumeAll();

        public void SetMasterVolume(float value) => SetMixerVolume("MasterVolume", value);
        public void SetMixerVolume(string parameter, float value)
        {
            if (mMixerHandle == null) return;
            var db = value <= 0f ? -80f : 20f * Mathf.Log10(Mathf.Clamp01(value));
            if (!mMixerHandle.Asset.SetFloat(parameter, db))
                Debug.LogWarning("Mixer 未暴露参数: " + parameter);
        }

        private bool TryGetCue(AudioCueKey key, out LoadedGroup group,
            out AudioResourceGroup.Entry cue)
        {
            cue = null;
            group = null;
            if (mDisposed || string.IsNullOrWhiteSpace(key.Group) ||
                !mGroups.TryGetValue(key.Group, out group) || group.Users == 0)
            {
                Debug.LogWarning("音频组尚未加载: " + key.Group);
                return false;
            }
            if (!group.Cues.TryGetValue(key.Cue, out cue))
            {
                Debug.LogWarning("音频 ID 不存在: " + key.Group + "/" + key.Cue);
                return false;
            }
            return true;
        }

        private void Finished(string key, LoadedGroup group)
        {
            // Driver 在自然结束或 Stop 时回调，随后才允许组释放。
            group.Playing--;
            ReleaseIfUnused(key, group);
        }

        private void ReleaseIfUnused(string key, LoadedGroup group)
        {
            if (group.Users > 0 || group.Playing > 0) return;
            mGroups.Remove(key);
            group.Handle.Release();
        }

        public void Deinit()
        {
            if (mDisposed) return;
            mDisposed = true;
            // 先停止播放，再释放 Clip 所在的组和 Mixer Handle。
            mDriver.StopAll();
            UnityEngine.Object.Destroy(mDriver.gameObject);
            mDriver = null;
            var groups = new List<LoadedGroup>(mGroups.Values);
            mGroups.Clear();
            foreach (var group in groups) group.Handle.Release();
            foreach (var key in new List<string>(mPending.Keys)) NotifyLoaded(key, false);
            mMixerHandle.Release();
            mMixerHandle = null;
        }
    }
}
