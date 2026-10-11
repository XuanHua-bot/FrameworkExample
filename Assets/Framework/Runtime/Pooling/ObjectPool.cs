using System;
using System.Collections.Generic;

namespace GameFramework
{
    public interface IObjectPool<T> where T : class
    {
        int CountAll { get; }
        int CountActive { get; }
        int CountInactive { get; }
        T Get();
        void Release(T item);
        void Prewarm(int count);
        void ClearInactive();
    }

    /// <summary>
    /// Reuses reference objects created by a caller-supplied factory.
    /// This class is Unity-independent and can also pool plain C# objects.
    /// </summary>
    public sealed class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private readonly Stack<T> mInactiveItems = new Stack<T>();
        private readonly HashSet<T> mInactiveSet = new HashSet<T>();
        private readonly HashSet<T> mKnownItems = new HashSet<T>();//记录当前对象池创建过的全部对象，包括活动对象和空闲对象
        private readonly Func<T> mCreateItem;
        private readonly Action<T> mOnGet;
        private readonly Action<T> mOnRelease;
        private readonly Action<T> mOnDestroy;
        private int mCountAll;

        public int CountAll => mCountAll;
        public int CountActive => CountAll - CountInactive;
        public int CountInactive => mInactiveItems.Count;

        public ObjectPool(
            Func<T> createItem,
            Action<T> onGet = null,
            Action<T> onRelease = null,
            Action<T> onDestroy = null,
            int defaultCapacity = 0)
        {
            mCreateItem = createItem ?? throw new ArgumentNullException(nameof(createItem));
            mOnGet = onGet;
            mOnRelease = onRelease;
            mOnDestroy = onDestroy;
            Prewarm(defaultCapacity);
        }

        public T Get()
        {
            var item = CountInactive > 0 ? mInactiveItems.Pop() : CreateItem();
            mInactiveSet.Remove(item);
            mOnGet?.Invoke(item);
            return item;
        }

        public void Release(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (!mKnownItems.Contains(item))
            {
                throw new InvalidOperationException("The item to release was not created by this pool.");
            }
            if (!mInactiveSet.Add(item))
                throw new InvalidOperationException("The same item was released to this pool twice.");

            mOnRelease?.Invoke(item);
            mInactiveItems.Push(item);
        }

        public void Prewarm(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            while (CountAll < count)
                ReleaseNewItem();
        }

        public void ClearInactive()
        {
            while (mInactiveItems.Count > 0)
            {
                var item = mInactiveItems.Pop();
                mInactiveSet.Remove(item);
                mKnownItems.Remove(item);
                mOnDestroy?.Invoke(item);
                mCountAll--;
            }
        }

        private T CreateItem()
        {
            var item = mCreateItem();
            if (item == null)
                throw new InvalidOperationException("The pool factory returned null.");

            mCountAll++;
            mKnownItems.Add(item);
            return item;
        }

        private void ReleaseNewItem()
        {
            var item = CreateItem();
            mInactiveSet.Add(item);
            mOnRelease?.Invoke(item);
            mInactiveItems.Push(item);
        }
    }
}
