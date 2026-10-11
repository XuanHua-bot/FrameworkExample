using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 项目级文字效果库。
    /// 文本中的 fx ID 在这里统一映射到一个或多个效果资源。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Framework/UI Text Effects/Library")]
    public sealed class UITextEffectLibrary : ScriptableObject
    {
        public const string ResourcesPath = "UI/TextEffects/UITextEffectLibrary";

        private static UITextEffectLibrary sInstance;

        // Unity 进入 Play Mode 时清掉上一次运行留下的静态引用，确保读取最新资源表。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeCache()
        {
            sInstance = null;
        }

        [Tooltip("全局唯一的文字效果 ID，例如 highlight_quest、highlight_map")]
        [SerializeField]
        private List<UITextEffectBinding> mBindings =
            new List<UITextEffectBinding>();

        public IReadOnlyList<UITextEffectBinding> Bindings => mBindings;

        public static IReadOnlyList<UITextAnimationEffect> FindEffects(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            if (sInstance == null)
            {
                sInstance = Resources.Load<UITextEffectLibrary>(ResourcesPath);
            }

            if (sInstance == null)
            {
                return null;
            }

            foreach (var binding in sInstance.mBindings)
            {
                if (binding != null &&
                    string.Equals(binding.Id.Trim(), id.Trim(),
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return binding.Effects;
                }
            }

            return null;
        }
    }
}
