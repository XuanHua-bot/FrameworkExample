using UnityEngine;

namespace GameFramework
{
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Heartbeat")]
    public sealed class UITextHeartbeatEffect : UITextAnimationEffect
    {
        [SerializeField, Range(1f, 1.5f)] private float mPeakScale = 1.16f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            // 两次短促收缩后留出休止段。整段文字共用相位，避免每个字各跳各的。
            var phase = Mathf.Repeat(context.Elapsed / Mathf.Max(0.01f, context.Duration), 1f);
            var firstBeat = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(phase - 0.16f) / 0.09f), 2f);
            var secondBeat = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(phase - 0.34f) / 0.07f), 2f);
            state.Scale *= 1f + (mPeakScale - 1f) * (firstBeat + secondBeat * 0.55f);
        }
    }
}
