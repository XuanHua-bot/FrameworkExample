using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    public interface ITypeEventSystem
    {
        void Send<T>() where T : new();
        void Send<T>(T e);
        IUnRegister Register<T>(Action<T> onEvent);
        void UnRegister<T>(Action<T> onEvent);
        void Clear();
    }

    public interface IUnRegister
    {
        void UnRegister();
    }

    public interface IUnRegisterList
    {
        List<IUnRegister> UnRegisterList { get; }
    }

    public struct TypeEventSystemRegister<T> : IUnRegister
    {
        public ITypeEventSystem TypeEventSystem;
        public Action<T> OnEvent;

        public void UnRegister()
        {
            TypeEventSystem.UnRegister(OnEvent);
            TypeEventSystem = null;
            OnEvent = null;
        }
    }

    public class UnRegisterOnDestroyTrigger : MonoBehaviour
    {
        private readonly HashSet<IUnRegister> mUnRegisters = new HashSet<IUnRegister>();

        public void AddUnRegister(IUnRegister unRegister)
        {
            mUnRegisters.Add(unRegister);
        }

        private void OnDestroy()
        {
            foreach (var unRegister in mUnRegisters)
                unRegister.UnRegister();

            mUnRegisters.Clear();
        }
    }

    public static class UnRegisterExtension
    {
        public static IUnRegister UnRegisterWhenGameObjectDestroyed(
            this IUnRegister unRegister, GameObject gameObject)
        {
            var trigger = gameObject.GetComponent<UnRegisterOnDestroyTrigger>();
            if (trigger == null)
                trigger = gameObject.AddComponent<UnRegisterOnDestroyTrigger>();

            trigger.AddUnRegister(unRegister);
            return unRegister;
        }
        public static IUnRegister UnRegisterWhenGameObjectDisabled(
            this IUnRegister unRegister, GameObject gameObject)
        {
            var trigger = gameObject.GetComponent<UnRegisterOnDisableTrigger>();
            if (trigger == null)
                trigger = gameObject.AddComponent<UnRegisterOnDisableTrigger>();

            trigger.AddUnRegister(unRegister);
            return unRegister;
        }
    }

    public class TypeEventSystem : ITypeEventSystem
    {
        private interface IRegistrations
        {
        }

        private sealed class Registrations<T> : IRegistrations
        {
            public Action<T> OnEvent = e => { };
        }

        private readonly Dictionary<Type, IRegistrations> mEventRegistrations =
            new Dictionary<Type, IRegistrations>();

        public static readonly TypeEventSystem Global = new TypeEventSystem();

        public void Send<T>() where T : new()
        {
            Send(new T());
        }

        public void Send<T>(T e)
        {
            if (mEventRegistrations.TryGetValue(typeof(T), out var registrations))
                ((Registrations<T>)registrations).OnEvent(e);
        }

        public IUnRegister Register<T>(Action<T> onEvent)
        {
            if (!mEventRegistrations.TryGetValue(typeof(T), out var registrations))
            {
                registrations = new Registrations<T>();
                mEventRegistrations.Add(typeof(T), registrations);
            }

            ((Registrations<T>)registrations).OnEvent += onEvent;
            return new TypeEventSystemRegister<T>
            {
                TypeEventSystem = this,
                OnEvent = onEvent
            };
        }

        public void UnRegister<T>(Action<T> onEvent)
        {
            if (mEventRegistrations.TryGetValue(typeof(T), out var registrations))
                ((Registrations<T>)registrations).OnEvent -= onEvent;
        }

        public void Clear()
        {
            mEventRegistrations.Clear();//清理当前Architecture的本地事件
        }
    }

    public interface IOnEvent<T>
    {
        void OnEvent(T e);
    }

    public static class OnGlobalEventExtension
    {
        public static IUnRegister RegisterEvent<T>(this IOnEvent<T> self) where T : struct
        {
            return TypeEventSystem.Global.Register<T>(self.OnEvent);
        }

        public static void UnRegisterEvent<T>(this IOnEvent<T> self) where T : struct
        {
            TypeEventSystem.Global.UnRegister<T>(self.OnEvent);
        }

        public static void AddToUnregisterList(this IUnRegister self, IUnRegisterList list)
        {
            list.UnRegisterList.Add(self);
        }

        public static void UnRegisterAll(this IUnRegisterList list)
        {
            foreach (var unRegister in list.UnRegisterList)
            {
                unRegister.UnRegister();
                
            }
            list.UnRegisterList.Clear();
        }
    }

    public class UnRegisterOnDisableTrigger : MonoBehaviour
    {
        private readonly HashSet<IUnRegister> mUnRegisters = new HashSet<IUnRegister>();

        public void AddUnRegister(IUnRegister unRegister)
        {
            mUnRegisters.Add(unRegister);
            
        }

        private void OnDisable()
        {
            foreach (var unRegister in mUnRegisters)
            {   
                unRegister.UnRegister();
                
                
            }
            mUnRegisters.Clear();
        }
    }
}
