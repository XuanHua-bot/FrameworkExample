using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// Optional callbacks for components on a pooled GameObject.
    /// OnSpawned runs after the object becomes active; OnDespawned runs before it becomes inactive.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    public interface IGameObjectPool : IUtility
    {
        void Prewarm(GameObject prefab, int count);
        GameObject Spawn(GameObject prefab, Transform parent = null);
        GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);
        bool Despawn(GameObject instance);
        int CountAll(GameObject prefab);
        int CountActive(GameObject prefab);
        void ClearInactive(GameObject prefab);//处理 prefab空闲实例
        void ClearAllInactive();//所有prefab的空闲时里
        void ClearAll();//所有活动于空闲实例， 所有池记录 整个框架会话结束
    }

    /// <summary>
    /// Prefab-keyed GameObject pools. An instance must return to the same service that spawned it.
    /// </summary>
    public sealed class GameObjectPool : IGameObjectPool,IDeinit
    {
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> mPools =
            new Dictionary<GameObject, ObjectPool<GameObject>>();
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> mActiveInstances =
            new Dictionary<GameObject, ObjectPool<GameObject>>();
        private readonly Transform mRoot;

        public GameObjectPool(Transform root = null)
        {
            mRoot = root;
        }

        public void Prewarm(GameObject prefab, int count)
        {
            GetOrCreatePool(prefab).Prewarm(count);
        }

        public GameObject Spawn(GameObject prefab, Transform parent = null)
        {
            return Spawn(prefab, Vector3.zero, Quaternion.identity, parent);
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var pool = GetOrCreatePool(prefab);
            var instance = pool.Get();

            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            InvokePoolables(instance, spawned: true);

            mActiveInstances.Add(instance, pool);
            return instance;
        }

        public bool Despawn(GameObject instance)
        {
            if (instance == null || !mActiveInstances.TryGetValue(instance, out var pool))
                return false;

            mActiveInstances.Remove(instance);
            InvokePoolables(instance, spawned: false);
            instance.SetActive(false);
            instance.transform.SetParent(mRoot, false);
            pool.Release(instance);
            return true;
        }

        public int CountAll(GameObject prefab)
        {
            return mPools.TryGetValue(prefab, out var pool) ? pool.CountAll : 0;
        }

        public int CountActive(GameObject prefab)
        {
            return mPools.TryGetValue(prefab, out var pool) ? pool.CountActive : 0;
        }

        public void ClearInactive(GameObject prefab)
        {
            if (mPools.TryGetValue(prefab, out var pool))
                pool.ClearInactive();
        }

        public void ClearAllInactive()
        {
            foreach (var pool in mPools.Values)
                pool.ClearInactive();
        }

        public void ClearAll()
        {
            //吧字典的键复制出来 ， 不能一边foreach 一边删除
            var activeInstances = new List<GameObject>(mActiveInstances.Keys);
            mActiveInstances.Clear();
            //取消活动对象->所属池 的映射 随后ondespawned
            foreach (var instance in activeInstances)
            {
                if (instance==null)
                {
                    continue;
                }
                //先通知 ipoolable.OnDespawned 然后再真正destory
                InvokePoolables(instance, spawned: false);
                DestroyInstance(instance);
            }
            //清理剩余对象
            foreach (var pool in mPools.Values) pool.ClearInactive();
            mPools.Clear();
        }

        private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            if (mPools.TryGetValue(prefab, out var pool))
                return pool;

            pool = new ObjectPool<GameObject>(
                () => CreateInstance(prefab),
                onDestroy: DestroyInstance);
            mPools.Add(prefab, pool);
            return pool;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            var instance = UnityEngine.Object.Instantiate(prefab, mRoot);
            instance.SetActive(false);
            return instance;
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (instance != null)
                UnityEngine.Object.Destroy(instance);
        }

        private static void InvokePoolables(GameObject instance, bool spawned)
        {
            var behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IPoolable poolable)
                {
                    if (spawned)
                        poolable.OnSpawned();
                    else
                        poolable.OnDespawned();
                }
            }
        }

        public void Deinit()
        {
            ClearAll();//销毁空闲实力 和仍处于活动状态的实例, 清理掉再mpools mActiveInstances
        }
    }
}
