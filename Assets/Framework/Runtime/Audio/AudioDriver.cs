using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

namespace GameFramework
{
    public class AudioDriver:MonoBehaviour
    {
        private const int MaxOneShots = 24;

        private sealed class Voice//一个voice 对应正在播放的短音频 和可回收的 AduioSource
        {
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
        //复用项目已有的普通对象池；GameObjectPool 适合 Prefab，不适合这里动态创建的 AudioSource。
        private IObjectPool<AudioSource> mVoicePool;
        //两个音乐槽 用于交叉淡化 不能用 一个Source同时淡入和淡出
        private readonly MusicSlot[] mMusic = { new MusicSlot(), new MusicSlot() };
        private int mCurrentMusic = -1;
        private int mNextId = 1;
        private bool mPaused;
        private Sequence mMusicTransition;


        public static AudioDriver Create(AudioMixer mixer)
        {
            //AduioRoot跨场景存在, Service销毁时一并销毁
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
            if (groups.Length==0)
                throw new InvalidOperationException("Mixer 缺少分组:" + name);
            return groups[0];
        }

        public int PlayOneShot(AudioClip clip, AudioMixerGroup outputGroup,
            float volume, Action finished)
        {
            //上线 避免错误调用导致无限创建AudioSource组件
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
            mVoices .Add(voice.Id,voice);
            voice.Wait = StartCoroutine(WaitForVoice(voice.Id));
            return voice.Id;
        }

        private IEnumerator WaitForVoice(int id)
        {
            var voice = mVoices[id];
            yield return null;//等待AduioSource进入isPlaying状态
            while (mPaused || voice.Source.isPlaying) yield return null;
            FinishVoice(id);

        }

        public void Stop(int id)
        {
            if (!mVoices.TryGetValue(id, out var voice)) return;
            if (voice.Wait!=null)StopCoroutine(voice.Wait);
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
            //上一次交叉淡化尚未结束时, 先清理两条旧music
            if (mMusicTransition != null) StopMusicImmediate();
            //当前槽之外的另一个槽作为新曲  旧曲仍保留到单出完成
            var next = mCurrentMusic == 0 ? 1 : 0;
            var old = mCurrentMusic;
            var incoming = mMusic[next];
            ReleaseMusic(incoming);
            incoming.Source.clip = clip;
            incoming.Source.loop = true;
            incoming.Source.volume = fade>0f?0f:volume;
            incoming.Finished = finished;
            incoming.Source.Play();
            if (mPaused)incoming.Source.Pause();
            mCurrentMusic = next;

            if (fade<=0f)
            {
                if (old >= 0) ReleaseMusic(mMusic[old]);
                return;
            }

            var transition = DOTween.Sequence().SetUpdate(true);
            transition.Join(incoming.Source.DOFade(volume, fade));
            if (old>=0) transition.Join(mMusic[old].Source.DOFade(0f, fade));
            mMusicTransition = transition.OnComplete(() =>
            {
                if (old >= 0) ReleaseMusic(mMusic[old]);
                mMusicTransition = null;
            });
            if (mPaused) mMusicTransition.Pause();
            
        }

        public void StopMusic(float fade)
        {
            if (mMusicTransition!=null)
            {
                StopMusicImmediate();
                return;
            }

            if (mCurrentMusic<0)return;
            var slot = mMusic[mCurrentMusic];
            mCurrentMusic = -1;
            if (fade<=0f)
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
            if (slot.Source==null||slot.Source.clip==null)return;
            slot.Source.Stop();
            slot.Source.clip = null;
            var finished = slot.Finished;
            slot.Finished = null;
            finished?.Invoke();
        }

        public void PauseAll()
        {
            if (mPaused)return;
            mPaused = true;
            foreach (var voice in mVoices.Values)voice.Source.Pause();
            foreach (var slot in mMusic)slot.Source.Pause();
            mMusicTransition?.Pause();
            
        }

        public void ResumeAll()
        {
            if (!mPaused) return;
            mPaused = false;
            foreach (var voice in mVoices.Values)voice.Source.UnPause();
            foreach (var slot in mMusic)slot.Source.UnPause();
            mMusicTransition?.Play();
        }

        public void StopAll()
        {
            var ids = new List<int>(mVoices.Keys);
            foreach (var id in ids)Stop(id);
            StopMusicImmediate();
        }

        private void OnDestroy()
        {
            StopAll();
        }

        private AudioSource NewSource(AudioMixerGroup group)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = group;
            return source;
        }
    }
}
