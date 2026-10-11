using System;
using UnityEngine;

using UnityEngine.Audio;

namespace GameFramework
{
    [CreateAssetMenu(menuName = "GameFramework/Audio Resource Group")]
    public sealed class AudioResourceGroup : ScriptableObject
    {
        [SerializeField] private AudioMixerGroup mOutputGroup;

        public AudioMixerGroup OutputGroup => mOutputGroup;

        [Serializable]
        public sealed class  Entry
        {
            public string Id;//id不必是文件名, 此处是 业务用 音频事件名
            public AudioClip Clip;
            //单条声音的相对音量, 分组音量由 AudioMixer控制
            [Range(0f, 1f)] public float Volume = 1f;
        }

        [SerializeField]private Entry[] mEntries;
        public Entry[] Entries=> mEntries;

    }
}
