using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameFramework.EditorTools
{
    /// <summary>
    /// 编辑器内的文字效果离线预览（只用于调参，不参与运行时，也不进入构建）。
    /// 用真实的文字效果资产在 Edit Mode 下逐帧计算并渲染成一张横向帧条 PNG，
    /// 这样检查动画形状不需要进入 Play Mode，也不受编辑器焦点影响。
    /// </summary>
    public static class UITextEffectPreviewTool
    {
        private const string FontPath = "Assets/Example/Front/TikTokFront SDF.asset";

        /// <summary>
        /// 把一段带 fx 标签的文本按时间采样渲染成横向帧条 PNG，返回输出文件的绝对路径。
        /// </summary>
        public static string RenderStrip(
            string markup,
            float duration,
            int frames,
            int frameWidth,
            int frameHeight,
            float fontSize,
            string outputFile)
        {
            var previousScene = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);

                var camGo = new GameObject("PreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.GetComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f, 1f);
                cam.transform.position = new Vector3(0f, 0f, -100f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 500f;
                cam.orthographicSize = frameHeight * 0.5f;
                cam.cullingMask = ~0;

                var canvasGo = new GameObject("PreviewCanvas", typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 50f;

                var textGo = new GameObject("PreviewText", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                textGo.transform.SetParent(canvasGo.transform, false);
                var rect = (RectTransform)textGo.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(frameWidth - 12, frameHeight - 12);

                var text = textGo.GetComponent<TextMeshProUGUI>();
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (font != null)
                {
                    text.font = font;
                }

                text.fontSize = fontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.enableWordWrapping = false;

                var parsed = UITextMarkupParser.Parse(markup);
                text.text = parsed.Text;
                Canvas.ForceUpdateCanvases();
                text.ForceMeshUpdate(true);
                var baseMesh = text.textInfo.CopyMeshInfoVertexData();

                // 与 Animator 的 BuildCharacterRangeCache 保持同样的时长规则。
                foreach (var range in parsed.Ranges)
                {
                    var count = Mathf.Max(0, range.EndExclusive - range.Start);
                    range.EffectDuration = 0.01f;
                    foreach (var effect in range.Effects)
                    {
                        if (effect != null)
                        {
                            range.EffectDuration = Mathf.Max(range.EffectDuration, effect.GetDuration(count, 0));
                        }
                    }
                }

                var rt = new RenderTexture(frameWidth, frameHeight, 24);
                var frameTex = new Texture2D(frameWidth, frameHeight, TextureFormat.RGBA32, false);
                var strip = new Texture2D(frameWidth * frames, frameHeight, TextureFormat.RGBA32, false);
                cam.targetTexture = rt;

                for (var frame = 0; frame < frames; frame++)
                {
                    var time = frames <= 1 ? 0f : duration * frame / (frames - 1);
                    ApplyFrame(text, baseMesh, parsed, time);
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    RenderTexture.active = rt;
                    frameTex.ReadPixels(new Rect(0, 0, frameWidth, frameHeight), 0, 0);
                    frameTex.Apply();
                    RenderTexture.active = null;
                    strip.SetPixels32(frame * frameWidth, 0, frameWidth, frameHeight, frameTex.GetPixels32());
                }

                strip.Apply();
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(frameTex);

                var fullPath = Path.GetFullPath(outputFile);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllBytes(fullPath, strip.EncodeToPNG());
                Object.DestroyImmediate(strip);
                return fullPath;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (!string.IsNullOrEmpty(previousScene))
                {
                    SceneManager.SetActiveScene(SceneManager.GetSceneByPath(previousScene));
                }
            }
        }

        private static void ApplyFrame(
            TMP_Text text,
            TMP_MeshInfo[] baseMesh,
            UITextMarkupResult parsed,
            float time)
        {
            var textInfo = text.textInfo;
            var textRect = text.rectTransform.rect;
            var textCenter = textRect.center;
            var textSize = textRect.size;

            for (var characterIndex = 0; characterIndex < textInfo.characterCount; characterIndex++)
            {
                var character = textInfo.characterInfo[characterIndex];
                if (!character.isVisible)
                {
                    continue;
                }

                var materialIndex = character.materialReferenceIndex;
                var vertexIndex = character.vertexIndex;
                var baseVertices = baseMesh[materialIndex].vertices;
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
                    Color = baseMesh[materialIndex].colors32[vertexIndex]
                };
                state.ColorBottom = state.Color;

                foreach (var range in parsed.Ranges)
                {
                    if (characterIndex < range.Start || characterIndex >= range.EndExclusive)
                    {
                        continue;
                    }

                    var elapsed = range.Playback.GetElapsed(time, range.EffectDuration);
                    var context = new UITextAnimationContext(
                        characterIndex, range.Start, range.EndExclusive, elapsed, range.EffectDuration, 0,
                        characterCenter, textCenter, textSize, character.lineNumber);
                    foreach (var effect in range.Effects)
                    {
                        if (effect != null)
                        {
                            effect.Apply(ref state, context);
                        }
                    }
                }

                var center = characterCenter;
                for (var vertex = 0; vertex < 4; vertex++)
                {
                    var scaled = baseVertices[vertexIndex + vertex] - center;
                    scaled.x *= state.Scale * state.ScaleXY.x;
                    scaled.y *= state.Scale * state.ScaleXY.y;
                    scaled.x += scaled.y * state.Skew;
                    var rotated = Quaternion.AngleAxis(state.Rotation, Vector3.forward) * scaled;
                    vertices[vertexIndex + vertex] = center + state.Offset + rotated;
                    var color = state.Gradient && vertex >= 2 ? state.ColorBottom : state.Color;
                    color.a = state.Visible
                        ? (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * state.Alpha), 0, 255)
                        : (byte)0;
                    textInfo.meshInfo[materialIndex].colors32[vertexIndex + vertex] = color;
                }
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
