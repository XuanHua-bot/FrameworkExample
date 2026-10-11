using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 按字符逐步显示文本。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Typewriter")]
    public sealed class UITextTypewriterEffect : UITextAnimationEffect
    {
        [SerializeField, Min(0f)] private float mCharacterDelay = 0.07f;
        [SerializeField, Min(0f)] private float mFadeDuration = 0.12f;
        [SerializeField, Min(0f)] private float mPunctuationDelay = 0.2f;

        public override float GetDuration(int characterCount)
        {
            return GetDuration() + Mathf.Max(0, characterCount - 1) * mCharacterDelay;
        }

        public override float GetDuration(int characterCount, int punctuationCount)
        {
            return GetDuration(characterCount) + punctuationCount * mPunctuationDelay;
        }

        public override void Apply(ref UITextCharacterState state, UITextAnimationContext context)
        {
            var start = GetRevealTime(context.CharacterIndex - context.RangeStart, context.PunctuationCount);
            var progress = Mathf.Clamp01((context.Elapsed - start) / Mathf.Max(0.01f, mFadeDuration));
            state.Visible &= context.Elapsed >= start;
            state.Alpha *= progress;
            if (!state.Visible)
            {
                state.Alpha = 0f;
            }
        }

        public float GetRevealTime(int index, int punctuationCount)
        {
            return index * mCharacterDelay + punctuationCount * mPunctuationDelay;
        }

        public static bool IsPauseCharacter(char character)
        {
            return character == ',' || character == '.' || character == '!' || character == '?' ||
                character == '，' || character == '。' || character == '！' || character == '？' ||
                character == '：' || character == '；';
        }
    }
}
