using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 逻辑音量类别
    /// </summary>
    [System.Serializable]
    public struct AudioCueKey
    {
        //Group是资源加载边界, Cue作为资源名称只在该组内唯一
        [SerializeField]private string mGroup;
        [SerializeField]private string mCue;
        
        public string Group=> mGroup;
        public string Cue=> mCue;
        
        /// <summary>
        /// 两个字段均已配置时才可以用于播放
        /// </summary>
        public bool IsValid=>
            !string.IsNullOrWhiteSpace(mGroup)&&
            !string.IsNullOrWhiteSpace(mCue);

        public AudioCueKey(string group, string cue)
        {
            
            mGroup = group;
            mCue = cue;
        }

    }
}
