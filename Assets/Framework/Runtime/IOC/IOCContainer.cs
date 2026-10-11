using System;
using System.Collections.Generic;

namespace GameFramework
{
    public class IOCContainer
    {
        private readonly Dictionary<Type, object> mInstances = new Dictionary<Type, object>();

        public void Register<T>(T instance)
        {
            mInstances[typeof(T)] = instance;
        }

        public T Get<T>() where T : class
        {
            return mInstances.TryGetValue(typeof(T), out var instance)
                ? instance as T
                : null;
        }

        public void Clear()
        {
            mInstances.Clear();
        }
    }
}
