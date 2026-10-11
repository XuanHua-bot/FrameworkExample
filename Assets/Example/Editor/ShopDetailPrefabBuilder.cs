using System.Linq;
using GameFramework.Examples;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameFramework.Editor
{
    public static class ShopDetailPrefabBuilder
    {
        private const string Source = "Assets/Framework/UI/ComponentLibrary/Containers/Popups/Popup Base - Concave.prefab";
        private const string Output = "Assets/Example/Resources/UI/ShopDetailPage.prefab";

        [MenuItem("GameFramework/UI/Build Shop Detail Demo")]
        public static void Build()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (source == null) throw new System.InvalidOperationException($"Missing prefab: {Source}");

            var root = new GameObject("ShopDetailPage", typeof(RectTransform), typeof(CanvasGroup),
                typeof(ShopDetailPage), typeof(UIAudioBinder));
            try
            {
                var rootRect = (RectTransform)root.transform;
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
                backdrop.transform.SetParent(rootRect, false);
                var backdropRect = (RectTransform)backdrop.transform;
                backdropRect.anchorMin = Vector2.zero;
                backdropRect.anchorMax = Vector2.one;
                backdropRect.offsetMin = Vector2.zero;
                backdropRect.offsetMax = Vector2.zero;
                backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, .55f);

                var shell = (GameObject)PrefabUtility.InstantiatePrefab(source);
                shell.transform.SetParent(rootRect, false);
                var shellRect = (RectTransform)shell.transform;
                shellRect.sizeDelta = new Vector2(640f, 460f);
                shellRect.anchoredPosition = Vector2.zero;
                shellRect.localScale = Vector3.one * .6f;

                var title = shell.GetComponentsInChildren<TMP_Text>(true).First(text => text.name == "Title");
                var close = shell.GetComponentsInChildren<UIButton>(true).First(button => button.name == "CloseButton");
                title.text = "商品详情";

                var iconObject = new GameObject("ProductIcon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(shell.transform, false);
                var iconRect = (RectTransform)iconObject.transform;
                iconRect.sizeDelta = new Vector2(144f, 144f);
                iconRect.anchoredPosition = new Vector2(-190f, -24f);
                var icon = iconObject.GetComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                var infoObject = new GameObject("ProductInfo", typeof(RectTransform), typeof(TextMeshProUGUI));
                infoObject.transform.SetParent(shell.transform, false);
                var infoRect = (RectTransform)infoObject.transform;
                infoRect.sizeDelta = new Vector2(300f, 180f);
                infoRect.anchoredPosition = new Vector2(88f, -24f);
                var info = infoObject.GetComponent<TextMeshProUGUI>();
                info.font = title.font;
                info.fontSize = 30f;
                info.color = new Color(.96f, .91f, .78f);
                info.alignment = TextAlignmentOptions.MidlineLeft;
                info.raycastTarget = false;

                var page = new SerializedObject(root.GetComponent<ShopDetailPage>());
                page.FindProperty("mTitle").objectReferenceValue = title;
                page.FindProperty("mInfo").objectReferenceValue = info;
                page.FindProperty("mIcon").objectReferenceValue = icon;
                page.FindProperty("mClose").objectReferenceValue = close;
                page.ApplyModifiedPropertiesWithoutUndo();

                var audio = root.GetComponent<UIAudioBinder>();
                audio.Group = "Audio/CommonUI";
                audio.PageOpenSound = new AudioCueKey("Audio/CommonUI", "MenuOpen");
                audio.PageCloseSound = new AudioCueKey("Audio/CommonUI", "MenuClose");
                PrefabUtility.SaveAsPrefabAsset(root, Output);
                AssetDatabase.SaveAssets();
                Debug.Log($"Built {Output} with nested {Source}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
