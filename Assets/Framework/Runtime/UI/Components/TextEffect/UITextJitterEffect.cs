using UnityEngine;

namespace GameFramework
{
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Jitter")]
    public sealed class UITextJitterEffect : UITextAnimationEffect
    {
        [SerializeField] private float mAmplitude = 1.5f;
        [SerializeField, Min(0.01f)] private float mFrequency = 7f;

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var phase = context.Elapsed * mFrequency + context.CharacterIndex * 17.31f;
            state.Offset += new Vector3(Mathf.Sin(phase * 1.7f), Mathf.Cos(phase * 2.3f), 0f) * mAmplitude;
        }
    }
}
