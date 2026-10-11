using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameFramework.Editor
{
    public static class UIComponentLibraryValidator
    {
        [MenuItem("GameFramework/UI/Validate Component Library")]
        public static void ValidateLibrary()
        {
            var errors = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Framework/UI/ComponentLibrary" }))
                errors.AddRange(Validate(AssetDatabase.GUIDToAssetPath(guid)));
            foreach (var error in errors) Debug.LogError(error);
            Debug.Log($"UI component validation: {errors.Count} errors");
        }

        [MenuItem("GameFramework/UI/Validate Assembled Pages")]
        public static void ValidatePages()
        {
            var errors = new List<string>();
            int pages = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab.GetComponent<UIPage>() == null) continue;
                pages++;
                errors.AddRange(Validate(path));
            }
            foreach (var error in errors) Debug.LogError(error);
            Debug.Log($"UI page validation: {pages} pages, {errors.Count} errors");
        }

        public static List<string> Validate(string path)
        {
            var errors = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                        errors.Add($"{path}: {transform.name} has a missing script");

                foreach (var button in root.GetComponentsInChildren<UIButton>(true))
                    if (new SerializedObject(button).FindProperty("mLabel").objectReferenceValue == null &&
                        button.GetComponentsInChildren<TMP_Text>(true).Length == 1)
                        errors.Add($"{path}: {button.name}.mLabel is missing for its text child");

                foreach (var list in root.GetComponentsInChildren<UISimpleList>(true))
                    RequireReference(list, "mItemTemplate", path, errors);
                foreach (var input in root.GetComponentsInChildren<UIInputField>(true))
                {
                    var assigned = new SerializedObject(input).FindProperty("mInputField").objectReferenceValue as TMP_InputField;
                    if (assigned != null && assigned.transform != input.transform && !assigned.transform.IsChildOf(input.transform))
                        errors.Add($"{path}: {input.name} input target is outside its hierarchy");
                    if (assigned == null && input.GetComponent<TMP_InputField>() == null &&
                        input.GetComponentsInChildren<TMP_InputField>(true).Length != 1)
                        errors.Add($"{path}: {input.name} needs one TMP_InputField or an explicit target");
                }
                foreach (var group in root.GetComponentsInChildren<UITabGroup>(true))
                {
                    var serialized = new SerializedObject(group);
                    var items = serialized.FindProperty("mItems");
                    var panels = serialized.FindProperty("mContentPanels");
                    if (items.arraySize == 0 && group.GetComponentsInChildren<UITabItem>(true).Length == 0)
                        errors.Add($"{path}: {group.name} has no tab items");
                    for (int i = 0; i < items.arraySize; i++)
                        if (items.GetArrayElementAtIndex(i).objectReferenceValue == null)
                            errors.Add($"{path}: {group.name} has a null tab item at {i}");
                    if (panels.arraySize > 0 && panels.arraySize != items.arraySize)
                        errors.Add($"{path}: {group.name} content panel count differs from tab items");
                }
                foreach (var selector in root.GetComponentsInChildren<UIOptionSelector>(true))
                {
                    RequireReference(selector, "mPrevious", path, errors);
                    RequireReference(selector, "mNext", path, errors);
                    RequireReference(selector, "mLabel", path, errors);
                    if (new SerializedObject(selector).FindProperty("mOptions").arraySize == 0)
                        errors.Add($"{path}: {selector.name} has no options");
                }
                foreach (var list in root.GetComponentsInChildren<UIVirtualList>(true))
                {
                    RequireReference(list, "mItemTemplate", path, errors);
                    var serialized = new SerializedObject(list);
                    var scroll = serialized.FindProperty("mScrollRect").objectReferenceValue as ScrollRect;
                    if (scroll == null) scroll = list.GetComponent<ScrollRect>();
                    var content = serialized.FindProperty("mContent").objectReferenceValue as RectTransform;
                    if (content == null && scroll != null) content = scroll.content;
                    if (scroll == null || content == null)
                        errors.Add($"{path}: {list.name} needs ScrollRect and Content");
                    if (content != null && content.GetComponent<LayoutGroup>() != null)
                        errors.Add($"{path}: {content.name} LayoutGroup conflicts with virtual positioning");
                }
                foreach (var count in root.GetComponentsInChildren<UIItemCount>(true))
                {
                    if (count.GetComponent<UIListItem>() != null)
                        errors.Add($"{path}: {count.name} quantity must be on a child, not the item root");
                    var reference = new SerializedObject(count).FindProperty("mCountText").objectReferenceValue;
                    if (reference == null && count.GetComponent<TMPro.TMP_Text>() == null)
                        errors.Add($"{path}: {count.name} needs quantity text");
                }
                foreach (var view in root.GetComponentsInChildren<UIItemView>(true))
                {
                    var serialized = new SerializedObject(view);
                    if (serialized.FindProperty("mIcon").objectReferenceValue == null &&
                        serialized.FindProperty("mCount").objectReferenceValue == null &&
                        serialized.FindProperty("mTitle").objectReferenceValue == null)
                        errors.Add($"{path}: {view.name} has no presentation references");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return errors;
        }

        private static void RequireReference(Component component, string field, string path, List<string> errors)
        {
            if (new SerializedObject(component).FindProperty(field).objectReferenceValue == null)
                errors.Add($"{path}: {component.name}.{field} is missing");
        }
    }
}
