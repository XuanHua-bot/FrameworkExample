# 音频管理器实现教学：配置、播放与热更边界

## 要实现的结果

本篇只保留一份音频教程。手抄后可以按模块配置音效，在进入模块时异步加载，按 Key 播放 UI/SFX/语音，播放 BGM，调整 Mixer 分组音量，并在声音结束后释放资源。代码依赖当前框架已有的 `IAssetLoader`、`AssetHandle<T>`、`IUtility` 和 `IDeinit`。

**状态要分清：**本篇代码可用当前 `ResourcesAssetLoader` 验证本地流程；远程下载、版本清单和 Bundle 更新尚未实现。音频模块遵守可替换资源接口，但这不等于项目已经完成热更新。

## 一、目录

新增 `Assets/Framework/Runtime/Audio/`，包含：

```text
AudioCueKey.cs
AudioResourceGroup.cs
IAudioService.cs
AudioService.cs
AudioDriver.cs
```

示例资产放在：

```text
Assets/Example/Resources/Audio/MasterMixer.mixer
Assets/Example/Resources/Audio/CommonUI.asset
Assets/Example/Resources/Audio/MainMenuMusic.asset
```

`CommonUI` 只放少量常用 UI 音效。不要建立一张引用全游戏 Clip 的大表；大型语音按章节或更小的实际加载边界拆组。

## 二、定义播放 Key

文件：`Assets/Framework/Runtime/Audio/AudioCueKey.cs`

```csharp
using System;
using UnityEngine;

namespace GameFramework
{
[Serializable]
    public struct AudioCueKey
    {
        // Group 是资源加载边界，Cue 只在该组内唯一。
        [SerializeField] private string mGroup;
        [SerializeField] private string mCue;

        public string Group => mGroup;
        public string Cue => mCue;

        public AudioCueKey(string group, string cue)
        {
            // 代码调用和 Inspector 配置最终使用同一组 Key。
            mGroup = group;
            mCue = cue;
        }
    }
}
```

`Group` 是资源加载 Key，例如 `Audio/CommonUI`；`Cue` 是组内名称，例如 `Click`。Inspector 可填这两个字段。经常在代码中使用的 Key 放在示例工程的一个静态类里，避免各页面重复手写字符串：

```csharp
using GameFramework;

namespace GameFramework.Examples
{
    public static class ExampleAudioKeys
    {
        public static readonly AudioCueKey UiClick =
            new AudioCueKey("Audio/CommonUI", "Click");
        public static readonly AudioCueKey MenuMusic =
            new AudioCueKey("Audio/MainMenuMusic", "Main");
    }
}
```

这个静态类放在 `Assets/Example/Scripts/`，不放进通用框架。它是使用端的约定，不是音频服务的全局注册表。

## 三、配置音频组

文件：`Assets/Framework/Runtime/Audio/AudioResourceGroup.cs`

```csharp
using System;
using UnityEngine;

namespace GameFramework
{
    [CreateAssetMenu(menuName = "GameFramework/Audio Resource Group")]
    public sealed class AudioResourceGroup : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            // Id 不必是文件名；它是业务使用的逻辑音频事件名。
            public string Id;
            public AudioClip Clip;
            // 这是单条声音的相对音量，分组总音量由 AudioMixer 控制。
            [Range(0f, 1f)] public float Volume = 1f;
        }

        [SerializeField] private Entry[] mEntries;
        public Entry[] Entries => mEntries;
    }
}
```

这里的 `ScriptableObject` 只是为了 Inspector 配置方便，并非热更新机制。组资产直接引用 Clip，因此加载组会带入这些资源依赖；一个组的大小应对应实际使用范围。第一版一个 Cue 对应一个 Clip，随机变体等到基本链路通过验证后再加。

## 四、音频接口

文件：`Assets/Framework/Runtime/Audio/IAudioService.cs`

```csharp
using System;

namespace GameFramework
{
    public interface IAudioService : IUtility
    {
        void LoadGroupAsync(string group, Action<bool> completed);
        void UnloadGroup(string group);
        int Play(AudioCueKey key);
        void Stop(int playbackId);
        void PlayMusic(AudioCueKey key, float fadeSeconds = 0.25f);
        void StopMusic(float fadeSeconds = 0.2f);
        void PauseAll();
        void ResumeAll();
        void SetMasterVolume(float value);
        void SetMixerVolume(string exposedParameter, float value);
    }
}
```

`Play` 只播放**已加载**组里的短音频；成功返回播放 ID，失败返回 `-1`。需要持续播放的音乐走 `PlayMusic`。每次成功的 `LoadGroupAsync` 都要在所属功能退出时配对一次 `UnloadGroup`；服务会计数，最后一个使用者离开且声音播完后才释放。不要让每次按钮点击都发起资源请求。

## 五、建立 Unity AudioMixer

在 Unity 创建 `MasterMixer.mixer`，建立 `Music`、`UI`、`Sfx`、`Voice` 四个子组。把 Master 和四个子组的 Volume 分别暴露为下面的**精确参数名**：

```text
MasterVolume
MusicVolume
UIVolume
SfxVolume
VoiceVolume
```

初始值均为 `0 dB`。Mixer 资产路径必须是 `Assets/Example/Resources/Audio/MasterMixer.mixer`，对应加载 Key `Audio/MasterMixer`。运行时分组音量交给 Mixer，播放源自身的 `volume` 只表示这条声音的相对音量和音乐淡化进度。这样修改用户音量不会覆盖正在进行的淡化。

## 六、AudioDriver：实际播放与回收

文件：`Assets/Framework/Runtime/Audio/AudioDriver.cs`

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

namespace GameFramework
{
    public sealed class AudioDriver : MonoBehaviour
    {
        private const int MaxOneShots = 24;

        private sealed class Voice
        {
            // 一个 Voice 对应一个正在播放的短音频和一个可回收 AudioSource。
            public int Id;
            public AudioSource Source;
            public Coroutine Wait;
            public Action Finished;
        }

        private sealed class MusicSlot
        {
            public AudioSource Source;
            public Action Finished;
        }

        private readonly Dictionary<int, Voice> mVoices = new Dictionary<int, Voice>();
        // 播放结束后保留 AudioSource，避免每次点击都 AddComponent。
        // 复用框架已有的普通对象池；这里的对象是运行时动态创建的 AudioSource，
        // 因此不使用按 Prefab 管理的 GameObjectPool。
        private IObjectPool<AudioSource> mVoicePool;
        // 两个音乐槽用于交叉淡化，不能用一个 Source 同时淡入和淡出。
        private readonly MusicSlot[] mMusic = { new MusicSlot(), new MusicSlot() };
        private int mCurrentMusic = -1;
        private int mNextId = 1;
        private bool mPaused;
        private Sequence mMusicTransition;

        public static AudioDriver Create(AudioMixer mixer)
        {
            // AudioRoot 跨场景存在；AudioService 销毁时统一销毁它。
            var music = FindGroup(mixer, "Music");
            var root = new GameObject("AudioRoot");
            DontDestroyOnLoad(root);
            var driver = root.AddComponent<AudioDriver>();
            driver.mVoicePool = new ObjectPool<AudioSource>(
                () => driver.NewSource(null),
                onRelease: source =>
                {
                    source.Stop();
                    source.clip = null;
                    source.outputAudioMixerGroup = null;
                });
            driver.mMusic[0].Source = driver.NewSource(music);
            driver.mMusic[1].Source = driver.NewSource(music);
            return driver;
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            var groups = mixer.FindMatchingGroups(name);
            if (groups.Length == 0)
                throw new InvalidOperationException("Mixer 缺少分组: " + name);
            return groups[0];
        }

        private AudioSource NewSource(AudioMixerGroup group)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = group;
            return source;
        }

        public int PlayOneShot(AudioClip clip, AudioMixerGroup outputGroup, float volume, Action finished)
        {
            // 上限是保护措施，避免错误调用无限创建 AudioSource。
            if (mVoices.Count >= MaxOneShots) return -1;
            var source = mVoicePool.Get();
            source.outputAudioMixerGroup = outputGroup;
            source.clip = clip;
            source.volume = volume;
            source.loop = false;
            source.Play();
            if (mPaused) source.Pause();

            var voice = new Voice
            {
                Id = mNextId++,
                Source = source,
                Finished = finished
            };
            mVoices.Add(voice.Id, voice);
            voice.Wait = StartCoroutine(WaitForVoice(voice.Id));
            return voice.Id;
        }

        private IEnumerator WaitForVoice(int id)
        {
            var voice = mVoices[id];
            // 等一帧，确保 AudioSource.Play 后 isPlaying 已进入正确状态。
            yield return null;
            while (mPaused || voice.Source.isPlaying) yield return null;
            FinishVoice(id);
        }

        public void Stop(int id)
        {
            if (!mVoices.TryGetValue(id, out var voice)) return;
            if (voice.Wait != null) StopCoroutine(voice.Wait);
            voice.Source.Stop();
            FinishVoice(id);
        }

        private void FinishVoice(int id)
        {
            if (!mVoices.TryGetValue(id, out var voice)) return;
            mVoices.Remove(id);
            voice.Source.clip = null;
            mVoicePool.Release(voice.Source);
            voice.Finished?.Invoke();
        }

        public void PlayMusic(AudioClip clip, float volume, float fade, Action finished)
        {
            // 上一次交叉淡化尚未结束时，先完整清理两条旧曲。
            if (mMusicTransition != null) StopMusicImmediate();
            // 当前槽之外的另一个槽作为新曲，旧曲仍保留到淡出完成。
            var next = mCurrentMusic == 0 ? 1 : 0;
            var old = mCurrentMusic;
            var incoming = mMusic[next];
            ReleaseMusic(incoming);
            incoming.Source.clip = clip;
            incoming.Source.loop = true;
            incoming.Source.volume = fade > 0f ? 0f : volume;
            incoming.Finished = finished;
            incoming.Source.Play();
            if (mPaused) incoming.Source.Pause();
            mCurrentMusic = next;

            if (fade <= 0f)
            {
                if (old >= 0) ReleaseMusic(mMusic[old]);
                return;
            }

            var transition = DOTween.Sequence().SetUpdate(true);
            transition.Join(incoming.Source.DOFade(volume, fade));
            if (old >= 0)
                transition.Join(mMusic[old].Source.DOFade(0f, fade));
            mMusicTransition = transition.OnComplete(() =>
            {
                if (old >= 0) ReleaseMusic(mMusic[old]);
                mMusicTransition = null;
            });
            if (mPaused) mMusicTransition.Pause();
        }

        public void StopMusic(float fade)
        {
            if (mMusicTransition != null)
            {
                StopMusicImmediate();
                return;
            }
            if (mCurrentMusic < 0) return;
            var slot = mMusic[mCurrentMusic];
            mCurrentMusic = -1;
            if (fade <= 0f)
            {
                ReleaseMusic(slot);
                return;
            }
            mMusicTransition = DOTween.Sequence().SetUpdate(true)
                .Join(slot.Source.DOFade(0f, fade))
                .OnComplete(() =>
                {
                    ReleaseMusic(slot);
                    mMusicTransition = null;
                });
            if (mPaused) mMusicTransition.Pause();
        }

        private void StopMusicImmediate()
        {
            mMusicTransition?.Kill();
            mMusicTransition = null;
            ReleaseMusic(mMusic[0]);
            ReleaseMusic(mMusic[1]);
            mCurrentMusic = -1;
        }

        private static void ReleaseMusic(MusicSlot slot)
        {
            if (slot.Source == null || slot.Source.clip == null) return;
            slot.Source.Stop();
            slot.Source.clip = null;
            var finished = slot.Finished;
            slot.Finished = null;
            finished?.Invoke();
        }

        public void PauseAll()
        {
            if (mPaused) return;
            mPaused = true;
            // DOTween 淡化也必须暂停，否则音量仍会在暂停期间变化。
            foreach (var voice in mVoices.Values) voice.Source.Pause();
            foreach (var slot in mMusic) slot.Source.Pause();
            mMusicTransition?.Pause();
        }

        public void ResumeAll()
        {
            if (!mPaused) return;
            mPaused = false;
            foreach (var voice in mVoices.Values) voice.Source.UnPause();
            foreach (var slot in mMusic) slot.Source.UnPause();
            mMusicTransition?.Play();
        }

        public void StopAll()
        {
            var ids = new List<int>(mVoices.Keys);
            foreach (var id in ids) Stop(id);
            StopMusicImmediate();
        }

        private void OnDestroy() => StopAll();
    }
}
```

短音效每次占用一个可回收的 `AudioSource`，因此可以单条停止，并在自然结束时通知资源层。`MaxOneShots=24` 只是防止无限创建的起点，不能当成性能结论；以后依据目标设备测试决定是否加入优先级、抢占或更严格的上限。音乐使用两个 Source，正常切换时交叉淡化；连续快速切歌时先清理旧过渡，避免遗留第三首。

## 七、AudioService：加载与持有

文件：`Assets/Framework/Runtime/Audio/AudioService.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace GameFramework
{
    public sealed class AudioService : IAudioService, IDeinit
    {
        private sealed class LoadedGroup
        {
            // Handle 必须活到组内所有播放实例结束。
            public AssetHandle<AudioResourceGroup> Handle;
            public readonly Dictionary<string, AudioResourceGroup.Entry> Cues =
                new Dictionary<string, AudioResourceGroup.Entry>();
            // 多个页面可以共享同一组，按使用者配对 Load/Unload。
            public int Users;
            // 播放中的声音也算资源持有者。
            public int Playing;
        }

        private sealed class PendingGroup
        {
            public readonly List<Action<bool>> Listeners = new List<Action<bool>>();
            public int Users;
        }

        private readonly IAssetLoader mLoader;
        private readonly Dictionary<string, LoadedGroup> mGroups =
            new Dictionary<string, LoadedGroup>();
        private readonly Dictionary<string, PendingGroup> mPending =
            new Dictionary<string, PendingGroup>();
        private AssetHandle<AudioMixer> mMixerHandle;
        private AudioDriver mDriver;
        private AudioCueKey mMusicKey;
        private bool mHasMusic;
        private bool mDisposed;

        public AudioService(IAssetLoader loader)
        {
            mLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            mMixerHandle = loader.Load<AudioMixer>("Audio/MasterMixer");
            if (mMixerHandle == null)
                throw new InvalidOperationException("缺少 Audio/MasterMixer");
            try { mDriver = AudioDriver.Create(mMixerHandle.Asset); }
            catch
            {
                mMixerHandle.Release();
                throw;
            }
        }

        public void LoadGroupAsync(string group, Action<bool> completed)
        {
            if (string.IsNullOrWhiteSpace(group) || mDisposed)
            {
                completed?.Invoke(false);
                return;
            }
            if (mGroups.TryGetValue(group, out var loaded))
            {
                loaded.Users++;
                completed?.Invoke(true);
                return;
            }
            // 合并同一组的并发加载，避免重复请求和重复 Handle。
            if (mPending.TryGetValue(group, out var pending))
            {
                pending.Users++;
                pending.Listeners.Add(completed);
                return;
            }
            pending = new PendingGroup { Users = 1 };
            pending.Listeners.Add(completed);
            mPending.Add(group, pending);
            mLoader.LoadAsync<AudioResourceGroup>(group,
                handle => CompleteLoad(group, handle),
                reason => FailLoad(group, reason));
        }

        private void CompleteLoad(string group, AssetHandle<AudioResourceGroup> handle)
        {
            if (mDisposed || !mPending.TryGetValue(group, out var pending) ||
                pending.Users == 0)
            {
                handle.Release();
                NotifyLoaded(group, false);
                return;
            }
            // pending.Users 是加载期间的使用者数量，转移到已加载组。
            var loaded = new LoadedGroup { Handle = handle, Users = pending.Users };
            var entries = handle.Asset.Entries;
            if (entries != null)
            {
                foreach (var cue in entries)
                {
                    if (cue == null || cue.Clip == null ||
                        string.IsNullOrWhiteSpace(cue.Id)) continue;
                    if (loaded.Cues.ContainsKey(cue.Id))
                    {
                        Debug.LogWarning("音频组内 ID 重复: " + group + "/" + cue.Id);
                        continue;
                    }
                    loaded.Cues.Add(cue.Id, cue);
                }
            }
            mGroups.Add(group, loaded);
            NotifyLoaded(group, true);
        }

        private void FailLoad(string group, string reason)
        {
            if (!mDisposed) Debug.LogWarning("音频组加载失败: " + group + "，" + reason);
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
            if (mPending.TryGetValue(group, out var pending) && pending.Users > 0)
                pending.Users--;
            if (!mGroups.TryGetValue(group, out var loaded)) return;
            if (loaded.Users > 0) loaded.Users--;
            ReleaseIfUnused(group, loaded);
        }

        public int Play(AudioCueKey key)
        {
            if (!TryGetCue(key, out var group, out var cue)) return -1;
            // 先增加播放持有数，再交给 Driver；失败时立即回滚。
            group.Playing++;
            var id = mDriver.PlayOneShot(cue.Clip, group.Handle.Asset.OutputGroup, cue.Volume,
                () => Finished(key.Group, group));
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
        public void SetMixerVolume(string exposedParameter, float value)
        {
            SetMixerVolumeInternal(exposedParameter, value);
        }

        private void SetMixerVolumeInternal(string parameter, float value)
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
            foreach (var group in mGroups.Values) group.Handle.Release();
            mGroups.Clear();
            foreach (var key in new List<string>(mPending.Keys)) NotifyLoaded(key, false);
            mMixerHandle.Release();
            mMixerHandle = null;
        }
    }
}
```

`LoadGroupAsync` 和 `UnloadGroup` 成对计数。最后一个使用者离开后，如果仍有声音在播放，继续持有 `AssetHandle<AudioResourceGroup>`；最后一条声音结束后才释放。正在加载的组如果所有使用者都请求卸载，加载完成后立即释放。音频组是**运行时加载边界**；实际热更 Bundle 怎样打包由资源构建规则决定，两者不必一一对应。

## 八、注册与配置

文件：`Assets/Example/Scripts/ExampleArchitecture.cs`。在注册 `IAssetLoader` 后增加：

```csharp
RegisterUtility<IAudioService>(new AudioService(GetUtility<IAssetLoader>()));
```

`Architecture.Deinit()` 会逆序清理 Utility，所以音频服务会先释放自己的资源 Handle，再由资源加载器清理缓存。不要在普通页面关闭或普通场景切换时调用整个 Architecture 的 `Deinit()`。

在 Unity 编辑器中：

1. 创建第五节的 `MasterMixer.mixer`。
2. 右键 `Create > GameFramework > Audio Resource Group`，创建 `CommonUI.asset` 和 `MainMenuMusic.asset`。
3. `CommonUI` 添加 `Click`：OutputGroup=`UI`，Clip 为点击音效。
4. `MainMenuMusic` 添加 `Main`：OutputGroup=`Music`，Clip 为菜单音乐。
5. 将两个组资产放到 `Assets/Example/Resources/Audio/`。Clip 可以放在别的目录，但会作为组的依赖进入构建内容。

常用 UI 组在 UI 初始化阶段加载一次：

```csharp
var audio = ExampleArchitecture.Interface.GetUtility<IAudioService>();
audio.LoadGroupAsync("Audio/CommonUI", success =>
{
    if (success) audio.Play(ExampleAudioKeys.UiClick);
});
```

进入主菜单：

```csharp
audio.LoadGroupAsync("Audio/MainMenuMusic", success =>
{
    if (success) audio.PlayMusic(ExampleAudioKeys.MenuMusic);
});
```

离开菜单：

```csharp
audio.StopMusic();
audio.UnloadGroup("Audio/MainMenuMusic");
```

停掉单条短音效：

```csharp
var playbackId = audio.Play(ExampleAudioKeys.UiClick);
if (playbackId >= 0) audio.Stop(playbackId);
```

当前 `UIButton` 仍只发出 `SoundRequested`；按钮接入时把其字符串音效 ID 改为 `AudioCueKey`，由页面或绑定组件调用 `audio.Play(key)`。不要在每次点击时异步加载常用 UI 组。

## 九、热更新接入与验收边界

当前能验证的是：音频服务按资源 Key 异步取得组，播放期间持有 Handle，卸载时释放。若将来接入可远程更新的 `IAssetLoader` 实现，同一组 Key 可以来自新版资源包，播放 API 不变。

**尚须另做资源模块**：本地/远端版本清单、下载、校验、失败重试、包依赖和内容更新。更新正在播放的组时，先完成新版下载；旧版继续播放至安全时机；旧 Handle 释放后，再通过资源系统加载新版。`ResourcesAssetLoader` 本身不提供这些能力。本篇也没有假设 YooAsset 已经接入。

手抄后逐项验证：

1. Mixer 缺少分组或暴露参数时能明确发现配置错误。
2. 两次同时请求相同组，只发起一次组加载并分别回调。
3. 未加载组时播放返回 `-1`；已加载组能播放 UI、SFX、Voice。
4. 单条停止和自然结束后，播放实例回收；达到上限时拒绝新播放。
5. 播放中卸载组不会立即释放 Handle；停止后释放。
6. 正常切歌交叉淡化；快速连续切歌不遗留旧曲。
7. 暂停时短音效和音乐、淡化都暂停；恢复后继续。
8. Mixer 总音量和各分组音量有效；设为 0 不出现对数错误。
9. 场景切换仅有一个 `AudioRoot`；`Deinit()` 后销毁 Root、释放组和 Mixer Handle。
10. 用 Unity Profiler 测音频内存与同时播放数，再决定分组大小和播放上限。当前数值只是保护阈值，不是性能认证。

Unity Play 模式编译和上述运行行为需要在你手抄后验证。本篇文档本身不能证明远程热更链路已完成。
