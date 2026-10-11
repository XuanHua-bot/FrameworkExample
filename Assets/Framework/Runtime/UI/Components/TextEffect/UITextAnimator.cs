using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 基于 TMP 顶点数据播放全局和行内文字效果。
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class UITextAnimator : MonoBehaviour
    {
        private enum CursorColorMode { White, Black, Custom }

        [SerializeField] private TMP_Text mText;
        [SerializeField, Min(0.01f)] private float mDuration = 0.5f;//动画总时长
        [Tooltip("勾选后：启用时自动把本组件 TMP 上的文本（含 <fx> 标签）解析并播放，无需写代码。文案固定时推荐勾选；运行时换文案请调用 Play(文本)。")]
        [SerializeField] private bool mPlayOnEnable;
        [SerializeField] private bool mShowTypewriterCursor = true;
        [SerializeField] private CursorColorMode mCursorColorMode = CursorColorMode.White;
        [SerializeField] private Color mCustomCursorColor = Color.white;
        [Tooltip("组件级默认效果，会应用到文本中的每个可见字符；行内 fx 效果仍由文本标签控制。")]
        [SerializeField] private List<UITextAnimationEffect> mGlobalEffects = new List<UITextAnimationEffect>();

        private TMP_MeshInfo[] mBaseMeshInfo;//原始网格
        private List<UITextEffectRange> mRanges = new List<UITextEffectRange>();
        // 一个字符可以同时属于多个范围，例如外层 shake 和内层 color/fade。
        // 使用列表数组保留所有重叠范围，避免后写入的范围覆盖先写入的范围。
        private List<UITextEffectRange>[] mCharacterRanges;
        private int[] mPunctuationPrefix;
        private string mSourceText;//原始文本
        private bool mHasSourceText;//是否以保存原文
        private float mElapsed;//已播放时间
        private float mPlaybackDuration;
        //播放状态
        private bool mPlaying;
        private bool mPaused;
        private bool mHasLoopingRange;
        private Image mCursor;
        private UITextEffectRange mTypewriterRange;
        private UITextTypewriterEffect mTypewriterEffect;

        public bool IsPlaying => mPlaying;
        public bool IsPaused => mPaused;
        public event Action Completed;

        private void Reset()
        {
            mText = GetComponent<TMP_Text>();
        }

        private void Awake()
        {
            if (mText == null)
            {
                mText = GetComponent<TMP_Text>();
            }
        }

        private void OnEnable()
        {
            if (mPlayOnEnable)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop(true);
        }

        /// <summary>
        /// 使用 TMP 当前文本解析标签并播放。
        /// </summary>
        public void Play()
        {
            var text = mHasSourceText ? mSourceText : mText == null ? string.Empty : mText.text;
            Play(text);
        }

        /// <summary>
        /// 替换文本、解析 fx 标签并开始播放。
        /// </summary>
        public void Play(string text)
        {
            if (mText == null)
            {
                return;
            }

            mSourceText = text ?? string.Empty;
            mHasSourceText = true;
            var parsed = UITextMarkupParser.Parse(mSourceText);
            mRanges = parsed.Ranges;
            //去掉框架标签的文本交给tmp 并使其立即重新生成字符信息
            mText.text = parsed.Text;
            // 先让父布局确定文本宽度，再由 TMP 计算换行后的首选高度。
            RefreshLayout();

            // 布局可能改变字符位置，动画必须缓存最终排版的网格。
            mText.ForceMeshUpdate(true);
           //复制一份 没有被动画修改的网格  以他为基准计算
            mBaseMeshInfo = mText.textInfo.CopyMeshInfoVertexData();
            BuildCharacterRangeCache(mText.textInfo.characterCount);
            FindTypewriterRange();
            mPlaybackDuration = mDuration;
            foreach (var range in mRanges)
            {
                if (range != null)
                {
                    mPlaybackDuration = Mathf.Max(mPlaybackDuration, range.Duration);
                }
            }
            //初始化播放状态 并立即计算第一帧
            mElapsed = 0f;
            mPlaying = true;
            mPaused = false;
            ApplyFrame();
        }

        private void FindTypewriterRange()
        {
            mTypewriterRange = null;
            mTypewriterEffect = null;
            if (!mShowTypewriterCursor)
            {
                HideCursor();
                return;
            }

            foreach (var range in mRanges)
            {
                foreach (var effect in range.Effects)
                {
                    if (effect is UITextTypewriterEffect typewriter)
                    {
                        mTypewriterRange = range;
                        mTypewriterEffect = typewriter;
                        return;
                    }
                }
            }

            HideCursor();
        }

        /// <summary>
        /// 文本变化后重建最近的布局根，使 TMP 的首选高度传递给父级。
        /// 宽度和内边距由 UI prefab 配置，文字组件不修改父节点的尺寸。
        /// </summary>
        private void RefreshLayout()
        {
            Canvas.ForceUpdateCanvases();
            var layoutRoot = mText.rectTransform;
            var current = layoutRoot.parent as RectTransform;
            while (current != null)
            {
                if (current.GetComponent<ContentSizeFitter>() != null)
                {
                    layoutRoot = current;
                    break;
                }

                layoutRoot = current;
                current = current.parent as RectTransform;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
            mText.ForceMeshUpdate(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// 暂停当前动画并保留当前顶点状态。
        /// </summary>
        public void Pause()
        {
            if (mPlaying)
            {
                mPaused = true;
            }
        }

        /// <summary>
        /// 继续播放已暂停的动画。
        /// </summary>
        public void Resume()
        {
            if (mPlaying)
            {
                mPaused = false;
            }
        }

        /// <summary>
        /// 立即显示最终状态并触发完成事件。
        /// </summary>
        public void CompleteImmediately()
        {
            if (mBaseMeshInfo == null)
            {
                return;
            }

            mElapsed = mPlaybackDuration;
            ApplyFrame();
            mPlaying = false;
            mPaused = false;
            HideCursor();
            Completed?.Invoke();
        }

        /// <summary>
        /// 停止动画。reset 为 true 时恢复开始播放前的网格状态。
        /// </summary>
        public void Stop(bool reset)
        {
            mPlaying = false;
            mPaused = false;
            HideCursor();
            if (reset && mText != null && mBaseMeshInfo != null)
            {
                RestoreBaseMesh();
            }
        }

        private void Update()
        {
            if (!mPlaying || mPaused)
            {
                return;
            }

            mElapsed += Time.unscaledDeltaTime;
            ApplyFrame();
            if (mElapsed < mPlaybackDuration)
            {
                return;
            }

            if (mHasLoopingRange)
            {
                return;
            }

            mPlaying = false;
            HideCursor();
            Completed?.Invoke();
        }

        private void ApplyFrame()//逐字符计算状态
        {
            var textInfo = mText.textInfo;
            // 依赖位置的复杂效果需要文本自身的中心与尺寸（都在文本局部坐标下）。
            var textRect = mText.rectTransform.rect;
            var textCenter = textRect.center;
            var textSize = textRect.size;
            for (var characterIndex = 0; characterIndex < textInfo.characterCount; characterIndex++)
            {
                var character = textInfo.characterInfo[characterIndex];
                if (!character.isVisible)//假如字符没有 可见的四边形也就是 空格换行  就跳过
                {
                    continue;
                }

                var materialIndex = character.materialReferenceIndex;
                var vertexIndex = character.vertexIndex;
                var baseVertices = mBaseMeshInfo[materialIndex].vertices;
                var vertices = textInfo.meshInfo[materialIndex].vertices;
                var characterCenter = (baseVertices[vertexIndex] + baseVertices[vertexIndex + 2]) * 0.5f;
                var state = new UITextCharacterState
                {
                    Visible = true,
                    Alpha = 1f,
                    Scale = 1f,
                    ScaleXY = Vector2.one,
                    Skew = 0f,
                    Gradient = false,
                    Rotation = 0f,
                    Offset = Vector3.zero,
                    Color = mBaseMeshInfo[materialIndex].colors32[vertexIndex]
                };
                state.ColorBottom = state.Color;
                var globalContext = new UITextAnimationContext(
                    characterIndex, 0, textInfo.characterCount, mElapsed, mPlaybackDuration,
                    0, characterCenter, textCenter, textSize, character.lineNumber);
                ApplyEffects(mGlobalEffects, ref state, globalContext);

                if (mCharacterRanges != null && characterIndex < mCharacterRanges.Length &&
                    mCharacterRanges[characterIndex] != null)
                {
                    foreach (var range in mCharacterRanges[characterIndex])
                    {
                        var elapsed = range.Playback.GetElapsed(mElapsed, range.EffectDuration);
                        var punctuationCount = mPunctuationPrefix[characterIndex] -
                            mPunctuationPrefix[Mathf.Clamp(range.Start, 0, characterIndex)];
                        var context = new UITextAnimationContext(
                            characterIndex, range.Start, range.EndExclusive,
                            elapsed, range.EffectDuration, punctuationCount,
                            characterCenter, textCenter, textSize, character.lineNumber);
                        ApplyEffects(range.Effects, ref state, context);
                    }
                }

                var center = characterCenter;
                for (var vertex = 0; vertex < 4; vertex++)
                {
                    // 支持非等比缩放（挤压/拉伸）与斜切：弹跳落位、翻转入场、数码撕裂都依赖它们。
                    var scaled = baseVertices[vertexIndex + vertex] - center;
                    scaled.x *= state.Scale * state.ScaleXY.x;
                    scaled.y *= state.Scale * state.ScaleXY.y;
                    scaled.x += scaled.y * state.Skew;
                    var rotated = Quaternion.AngleAxis(state.Rotation, Vector3.forward) * scaled;
                    vertices[vertexIndex + vertex] = center + state.Offset + rotated;
                    // 顶点顺序是 左上/右上/右下/左下，因此 2、3 号顶点属于下半部分。
                    var color = state.Gradient && vertex >= 2 ? state.ColorBottom : state.Color;
                    color.a = state.Visible ? (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * state.Alpha), 0, 255) : (byte)0;
                    textInfo.meshInfo[materialIndex].colors32[vertexIndex + vertex] = color;
                }
            }

            mText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
            UpdateCursor();
        }

        private void UpdateCursor()
        {
            if (mTypewriterRange == null || mTypewriterEffect == null)
            {
                return;
            }

            var elapsed = mTypewriterRange.Playback.GetElapsed(mElapsed, mTypewriterRange.EffectDuration);
            if (elapsed >= mTypewriterRange.EffectDuration)
            {
                HideCursor();
                return;
            }

            var info = mText.textInfo;
            var start = Mathf.Clamp(mTypewriterRange.Start, 0, info.characterCount);
            var end = Mathf.Clamp(mTypewriterRange.EndExclusive, start, info.characterCount);
            var lastVisible = -1;
            var nextCharacter = -1;
            for (var i = start; i < end; i++)
            {
                var pauses = mPunctuationPrefix[i] - mPunctuationPrefix[start];
                if (elapsed < mTypewriterEffect.GetRevealTime(i - start, pauses))
                {
                    nextCharacter = i;
                    break;
                }

                if (info.characterInfo[i].isVisible)
                {
                    lastVisible = i;
                }
            }

            if (start == end)
            {
                HideCursor();
                return;
            }

            if (mCursor == null)
            {
                var cursorObject = new GameObject("Typewriter Caret", typeof(RectTransform),
                    typeof(Image), typeof(LayoutElement));
                // 光标与 TMP 文字使用同一个父节点，避免把 TMP 的局部坐标直接当成子节点坐标。
                cursorObject.transform.SetParent(mText.rectTransform.parent, false);
                cursorObject.GetComponent<LayoutElement>().ignoreLayout = true;
                mCursor = cursorObject.GetComponent<Image>();
                mCursor.raycastTarget = false;
                var rect = mCursor.rectTransform;
                rect.anchorMin = mText.rectTransform.pivot;
                rect.anchorMax = mText.rectTransform.pivot;
                rect.pivot = new Vector2(0f, 0.5f);
                cursorObject.transform.SetAsLastSibling();
            }

            var cursorIndex = nextCharacter >= 0 ? nextCharacter : lastVisible;
            if (cursorIndex < 0)
            {
                HideCursor();
                return;
            }

            if (nextCharacter >= 0)
            {
                while (cursorIndex < end - 1 && !info.characterInfo[cursorIndex].isVisible)
                {
                    cursorIndex++;
                }
            }

            var character = info.characterInfo[cursorIndex];
            var x = nextCharacter >= 0 ? character.origin : character.xAdvance;
            var height = Mathf.Max(mText.fontSize * 0.8f, character.ascender - character.descender);
            var color = mCursorColorMode == CursorColorMode.Black ? Color.black :
                mCursorColorMode == CursorColorMode.Custom ? mCustomCursorColor : Color.white;
            color.a *= Mathf.Lerp(0.35f, 1f, Mathf.PingPong(mElapsed * 2f, 1f));
            mCursor.color = color;
            mCursor.rectTransform.sizeDelta = new Vector2(Mathf.Max(3f, mText.fontSize * 0.08f), height);
            var localPosition = mText.rectTransform.TransformPoint(
                new Vector3(x + 2f, (character.ascender + character.descender) * 0.5f));
            var parent = mCursor.rectTransform.parent as RectTransform;
            mCursor.rectTransform.localPosition = parent == null
                ? localPosition
                : parent.InverseTransformPoint(localPosition);
            mCursor.gameObject.SetActive(true);
        }

        private void HideCursor()
        {
            if (mCursor != null)
            {
                mCursor.gameObject.SetActive(false);
            }
        }

        private void ApplyEffects(List<UITextAnimationEffect> effects, ref UITextCharacterState state, UITextAnimationContext context)
        {
            if (effects == null)
            {
                return;
            }

            foreach (var effect in effects)
            {
                if (effect != null)
                {
                    effect.Apply(ref state, context);
                }
            }
        }

        private void BuildCharacterRangeCache(int characterCount)
        {
            mCharacterRanges = new List<UITextEffectRange>[characterCount];
            mPunctuationPrefix = new int[characterCount + 1];
            for (var i = 0; i < characterCount; i++)
            {
                mPunctuationPrefix[i + 1] = mPunctuationPrefix[i] +
                    (UITextTypewriterEffect.IsPauseCharacter(mText.textInfo.characterInfo[i].character) ? 1 : 0);
            }
            mHasLoopingRange = false;
            foreach (var range in mRanges)
            {
                if (range == null)
                {
                    continue;
                }

                // 只有无限循环需要让 Animator 持续运行；有限循环会在总时长结束后完成。
                mHasLoopingRange |= range.Playback != null && range.Playback.IsInfinite;
                var start = Mathf.Clamp(range.Start, 0, characterCount);
                var end = Mathf.Clamp(range.EndExclusive, start, characterCount);
                var punctuationCount = end > start ?
                    mPunctuationPrefix[end - 1] - mPunctuationPrefix[start] : 0;
                range.EffectDuration = 0.01f;
                foreach (var effect in range.Effects)
                {
                    if (effect != null)
                    {
                        range.EffectDuration = Mathf.Max(range.EffectDuration,
                            effect.GetDuration(end - start, punctuationCount));
                    }
                }
                range.Duration = range.Playback.GetTotalDuration(range.EffectDuration);
                for (var characterIndex = start; characterIndex < end; characterIndex++)
                {
                    var ranges = mCharacterRanges[characterIndex] ??
                        (mCharacterRanges[characterIndex] = new List<UITextEffectRange>());
                    ranges.Add(range);
                }
            }
        }

        private void RestoreBaseMesh()
        {
            var textInfo = mText.textInfo;
            for (var i = 0; i < textInfo.meshInfo.Length; i++)
            {
                Array.Copy(mBaseMeshInfo[i].vertices, textInfo.meshInfo[i].vertices, mBaseMeshInfo[i].vertices.Length);
                Array.Copy(mBaseMeshInfo[i].colors32, textInfo.meshInfo[i].colors32, mBaseMeshInfo[i].colors32.Length);
            }

            mText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
