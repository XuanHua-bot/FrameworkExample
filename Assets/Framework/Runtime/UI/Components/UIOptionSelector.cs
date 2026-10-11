using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GameFramework
{
    /// <summary>用左右按钮在预设选项中切换；不依赖原生 Slider。</summary>
    [DisallowMultipleComponent]
    public sealed class UIOptionSelector : MonoBehaviour
    {
        [SerializeField] private UIButton mPrevious;
        [SerializeField] private UIButton mNext;
        [SerializeField] private TMP_Text mLabel;
        [SerializeField] private TMP_Text mCounter;
        [SerializeField] private List<string> mOptions = new List<string>();
        [SerializeField] private bool mLoop = true;

        private int mIndex;

        public int Index => mIndex;
        public string Value => mOptions.Count == 0 ? string.Empty : mOptions[mIndex];
        public IReadOnlyList<string> Options => mOptions;
        public event Action<int, string, bool> Changed;

        private void OnEnable()
        {
            if (mPrevious != null) mPrevious.Clicked += Previous;
            if (mNext != null) mNext.Clicked += Next;
            Refresh();
        }

        private void OnDisable()
        {
            if (mPrevious != null) mPrevious.Clicked -= Previous;
            if (mNext != null) mNext.Clicked -= Next;
        }

        public void SetOptions(IList<string> options, int selectedIndex = 0)
        {
            mOptions.Clear();
            if (options != null) mOptions.AddRange(options);
            mIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, mOptions.Count - 1));
            Refresh();
        }

        public void Select(int index, bool notify = false)
        {
            if (index < 0 || index >= mOptions.Count || index == mIndex) return;
            mIndex = index;
            Refresh();
            if (notify) Changed?.Invoke(mIndex, Value, false);
        }

        private void Previous() => Step(-1);
        private void Next() => Step(1);

        private void Step(int direction)
        {
            if (mOptions.Count == 0) return;
            int next = mLoop ? (mIndex + direction + mOptions.Count) % mOptions.Count :
                Mathf.Clamp(mIndex + direction, 0, mOptions.Count - 1);
            if (next == mIndex) return;
            mIndex = next;
            Refresh();
            Changed?.Invoke(mIndex, Value, true);
        }

        private void Refresh()
        {
            if (mOptions.Count > 0) mIndex = Mathf.Clamp(mIndex, 0, mOptions.Count - 1);
            else mIndex = 0;
            if (mLabel != null) mLabel.text = Value;
            if (mCounter != null) mCounter.text = mOptions.Count == 0 ? "0/0" : $"{mIndex + 1}/{mOptions.Count}";
            if (mPrevious != null) mPrevious.SetInteractable(mOptions.Count > 0 && (mLoop || mIndex > 0));
            if (mNext != null) mNext.SetInteractable(mOptions.Count > 0 && (mLoop || mIndex < mOptions.Count - 1));
        }
    }
}
