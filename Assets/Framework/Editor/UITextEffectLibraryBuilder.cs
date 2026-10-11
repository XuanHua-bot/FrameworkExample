using UnityEditor;
using UnityEngine;

namespace GameFramework.Editor
{
    /// <summary>
    /// 创建文字动画的基础 Effect 资源和全局绑定库。
    /// 这是编辑器工具，不参与运行时构建。
    /// </summary>
    public static class UITextEffectLibraryBuilder
    {
        private const string RootPath = "Assets/Framework/Resources/UI/TextEffects";
        private const string EffectPath = RootPath + "/Effects";
        private const string LibraryPath = RootPath + "/UITextEffectLibrary.asset";

        [InitializeOnLoadMethod]
        private static void EnsureDefaultLibrary()
        {
            EditorApplication.delayCall += EnsureDefaultLibraryAfterImport;
        }

        private static void EnsureDefaultLibraryAfterImport()
        {
            var library = AssetDatabase.LoadAssetAtPath<UITextEffectLibrary>(LibraryPath);
            var bindings = library == null
                ? null
                : new SerializedObject(library).FindProperty("mBindings");
            if (library == null || bindings == null || bindings.arraySize == 0)
            {
                CreateDefaultLibrary();
            }
        }

        [MenuItem("Game Framework/UI Text Effects/Create Default Library")]
        public static void CreateDefaultLibrary()
        {
            EnsureFolder("Assets/Framework/Resources", "UI");
            EnsureFolder("Assets/Framework/Resources/UI", "TextEffects");
            EnsureFolder(RootPath, "Effects");

            var fade = GetOrCreate<UITextFadeInEffect>("FadeIn.asset");
            var scale = GetOrCreate<UITextScaleInEffect>("ScaleIn.asset");
            var shake = GetOrCreate<UITextShakeEffect>("Shake.asset");
            var floating = GetOrCreate<UITextFloatEffect>("Float.asset");
            var typewriter = GetOrCreate<UITextTypewriterEffect>("Typewriter.asset");
            var rotate = GetOrCreate<UITextRotateEffect>("Rotate.asset");
            var colorPulse = GetOrCreate<UITextColorPulseEffect>("ColorPulse.asset");
            var wave = GetOrCreate<UITextWaveEffect>("Wave.asset");
            var heartbeat = GetOrCreate<UITextHeartbeatEffect>("Heartbeat.asset");
            var bounce = GetOrCreate<UITextBounceEffect>("Bounce.asset");
            var rainbow = GetOrCreate<UITextRainbowEffect>("Rainbow.asset");
            var jitter = GetOrCreate<UITextJitterEffect>("Jitter.asset");
            var shineSweep = GetOrCreate<UITextShineSweepEffect>("ShineSweep.asset");
            var glitchBurst = GetOrCreate<UITextGlitchBurstEffect>("GlitchBurst.asset");
            var chargeStrike = GetOrCreate<UITextChargeStrikeEffect>("ChargeStrike.asset");
            var pulseScanner = GetOrCreate<UITextPulseScannerEffect>("PulseScanner.asset");
            var popIn = GetOrCreate<UITextPopInEffect>("PopIn.asset");
            var spinDrop = GetOrCreate<UITextSpinDropEffect>("SpinDrop.asset");
            var flipIn = GetOrCreate<UITextFlipInEffect>("FlipIn.asset");
            var flyIn = GetOrCreate<UITextFlyInEffect>("FlyIn.asset");
            var shatterSnap = GetOrCreate<UITextShatterSnapEffect>("ShatterSnap.asset");
            var glitchSlice = GetOrCreate<UITextGlitchSliceEffect>("GlitchSlice.asset");
            var gradientFlow = GetOrCreate<UITextGradientFlowEffect>("GradientFlow.asset");
            // 分阶段效果需要更长的默认时长，基类默认 0.2 秒只能闪一下就结束。
            EnsureDuration(shineSweep, 1.2f);
            EnsureDuration(chargeStrike, 1.6f);
            EnsureDuration(pulseScanner, 1.2f);
            EnsureDuration(popIn, 0.3f);
            EnsureDuration(spinDrop, 0.45f);
            EnsureDuration(flipIn, 0.35f);
            EnsureDuration(flyIn, 0.35f);
            EnsureDuration(shatterSnap, 0.5f);
            EnsureDuration(glitchSlice, 1f);
            EnsureDuration(gradientFlow, 2.4f);

            var library = AssetDatabase.LoadAssetAtPath<UITextEffectLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<UITextEffectLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var serialized = new SerializedObject(library);
            var bindings = serialized.FindProperty("mBindings");
            bindings.ClearArray();

            AddBinding(bindings, "fade_in", fade);
            AddBinding(bindings, "scale_in", scale);
            AddBinding(bindings, "typewriter", typewriter);
            AddBinding(bindings, "idle_shake", shake);
            AddBinding(bindings, "idle_float", floating);
            AddBinding(bindings, "idle_rotate", rotate);
            AddBinding(bindings, "idle_wave", wave);
            AddBinding(bindings, "color_pulse", colorPulse);
            AddBinding(bindings, "highlight_rare", colorPulse, rotate);
            AddBinding(bindings, "highlight_warning", colorPulse, shake);
            AddBinding(bindings, "heartbeat", heartbeat);
            AddBinding(bindings, "bounce", bounce);
            AddBinding(bindings, "rainbow", rainbow);
            AddBinding(bindings, "jitter", jitter);
            AddBinding(bindings, "reward_legendary", heartbeat, rainbow);
            AddBinding(bindings, "shine_sweep", shineSweep);
            AddBinding(bindings, "glitch_burst", glitchBurst);
            AddBinding(bindings, "charge_strike", chargeStrike);
            AddBinding(bindings, "pulse_scanner", pulseScanner);
            AddBinding(bindings, "pop_in", popIn);
            AddBinding(bindings, "spin_drop", spinDrop);
            AddBinding(bindings, "flip_in", flipIn);
            AddBinding(bindings, "fly_in", flyIn);
            AddBinding(bindings, "shatter_snap", shatterSnap);
            AddBinding(bindings, "glitch_slice", glitchSlice);
            AddBinding(bindings, "gradient_flow", gradientFlow);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Created default UITextEffectLibrary at " + LibraryPath);
        }

        /// <summary>
        /// 新效果资源会继承基类的 0.2 秒默认时长，对蓄力、扫描这类分阶段效果太短。
        /// 只在资源仍是基线默认值时写入，避免覆盖手动调过的参数。
        /// </summary>
        private static void EnsureDuration(UITextAnimationEffect effect, float duration)
        {
            var serialized = new SerializedObject(effect);
            var property = serialized.FindProperty("mDuration");
            if (property == null || property.floatValue > 0.21f)
            {
                return;
            }

            property.floatValue = duration;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(effect);
        }

        private static T GetOrCreate<T>(string fileName) where T : UITextAnimationEffect
        {
            var path = EffectPath + "/" + fileName;
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void AddBinding(
            SerializedProperty bindings,
            string id,
            params UITextAnimationEffect[] effects)
        {
            var index = bindings.arraySize;
            bindings.InsertArrayElementAtIndex(index);
            var binding = bindings.GetArrayElementAtIndex(index);
            binding.FindPropertyRelative("Id").stringValue = id;

            var effectList = binding.FindPropertyRelative("Effects");
            effectList.ClearArray();
            foreach (var effect in effects)
            {
                var effectIndex = effectList.arraySize;
                effectList.InsertArrayElementAtIndex(effectIndex);
                effectList.GetArrayElementAtIndex(effectIndex).objectReferenceValue = effect;
            }
        }

        private static void EnsureFolder(string parent, string folder)
        {
            var path = parent + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
