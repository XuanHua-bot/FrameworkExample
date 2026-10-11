using System;

namespace GameFramework
{
    public interface IAudioService:IUtility
    {
        void LoadGroupAsync(string group, Action<bool> completed);//退出时配对一次UnloadGroup
        void UnloadGroup(string group);
        int Play(AudioCueKey key);//只播放已加载组里的短音频，成功返回播放 ID，失败返回 -1
        void Stop(int playbackId);
        void PlayMusic(AudioCueKey key, float fadeSeconds = 0.25f);//持续播放的音乐
        void StopMusic(float fadeSeconds = 0.2f);
        void PauseAll();
        void ResumeAll();
        void SetMasterVolume(float value);
        void SetMixerVolume(string exposedParameter, float value);
    }
}
