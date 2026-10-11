using System;
using System.Collections.Generic;

namespace GameFramework
{
    public interface IArchitecture
    {
        void RegisterSystem<T>(T system) where T : ISystem;
        void RegisterModel<T>(T model) where T : IModel;
        void RegisterUtility<T>(T utility) where T : IUtility;
        T GetUtility<T>() where T : class, IUtility;
        T GetModel<T>() where T : class, IModel;
        T GetSystem<T>() where T : class, ISystem;
        void SendCommand<TCommand>() where TCommand : ICommand, new();
        void SendCommand<TCommand>(TCommand command) where TCommand : ICommand;
        TResult SendCommand<TResult>(ICommand<TResult> command  );
        TResult SendQuery<TResult>(IQuery<TResult> query);
        void SendEvent<T>() where T : new();
        void SendEvent<T>(T e);
        IUnRegister RegisterEvent<T>(Action<T> onEvent);
        void UnRegisterEvent<T>(Action<T> onEvent);
        /// <summary>
        /// 只有再 退出游戏绘画， 切换账号， 重置框架， 测试结束， 才 由architecture调用 ,间接触发对象池的ClearAll()
        /// </summary>
        void Deinit();
    }

    public abstract class Architecture<T> : IArchitecture
        where T : Architecture<T>, new()
    {
        private static T mArchitecture;
        
        private readonly IOCContainer mContainer = new IOCContainer();
        private readonly TypeEventSystem mEventSystem = new TypeEventSystem();
        
        //还没到同意初始化实际的对象
        private readonly List<IModel> mModels = new List<IModel>();
        private readonly List<ISystem> mSystems = new List<ISystem>();
        
        //已真正执行Init()的对象， 供Deinit()逆序使用
        private readonly List<IModel> mInitializedModels = new List<IModel>();
        private readonly List<ISystem> mInitializedSystems = new List<ISystem>();
        private readonly List<IUtility> mRegisteredUtilities = new List<IUtility>();
        
        private bool mInited;

        public static IArchitecture Interface
        {
            get
            {
                MakeSureArchitecture();
                return mArchitecture;
            }
        }

        public static Action<T> OnRegisterPatch = architecture => { };

        private static void MakeSureArchitecture()
        {
            if (mArchitecture != null)
                return;

            mArchitecture = new T();
            mArchitecture.Init();
            OnRegisterPatch?.Invoke(mArchitecture);

            foreach (var model in mArchitecture.mModels)
                mArchitecture.InitModel(model);
            mArchitecture.mModels.Clear();

            foreach (var system in mArchitecture.mSystems)
                mArchitecture.InitSystem(system);
            mArchitecture.mSystems.Clear();

            mArchitecture.mInited = true;
        }

        protected abstract void Init();

        public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem
        {
            system.SetArchitecture(this);
            mContainer.Register(system);

            if (mInited)
                InitSystem(system);
            else
                mSystems.Add(system);
        }

        public void RegisterModel<TModel>(TModel model) where TModel : IModel
        {
            model.SetArchitecture(this);
            mContainer.Register(model);

            if (mInited)
                InitModel(model);
            else
                mModels.Add(model);
        }

        public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility
        {
            mContainer.Register(utility);
            mRegisteredUtilities.Add(utility);
        }

        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility
        {
            return mContainer.Get<TUtility>();
        }

        public TModel GetModel<TModel>() where TModel : class, IModel
        {
            return mContainer.Get<TModel>();
        }

        public TSystem GetSystem<TSystem>() where TSystem : class, ISystem
        {
            return mContainer.Get<TSystem>();
        }

        

        public void SendCommand<TCommand>() where TCommand : ICommand, new()
        {
            SendCommand(new TCommand());
        }

        public void SendCommand<TCommand>(TCommand command) where TCommand : ICommand
        {
            command.SetArchitecture(this);
            command.Execute();
        }

        public TResult SendCommand<TResult>(ICommand<TResult> command)
        {   
            command.SetArchitecture(this);
            return command.Execute();
        }

        public TResult SendQuery<TResult>(IQuery<TResult> query)
        {
            query.SetArchitecture(this);
            return query.Do();
        }

        public void SendEvent<TEvent>() where TEvent : new()
        {
            mEventSystem.Send<TEvent>();
        }

        public void SendEvent<TEvent>(TEvent e)
        {
            mEventSystem.Send(e);
        }

        public IUnRegister RegisterEvent<TEvent>(Action<TEvent> onEvent)
        {
            return mEventSystem.Register(onEvent);
        }

        public void UnRegisterEvent<TEvent>(Action<TEvent> onEvent)
        {
            mEventSystem.UnRegister(onEvent);
        }

        public void Deinit()
        {
            if (!mInited)return;
            DeinitReverse(mInitializedSystems);
            DeinitReverse(mInitializedModels);
            DeinitReverse(mRegisteredUtilities);
            
            mEventSystem.Clear();
            mContainer.Clear();
            
            mInitializedSystems.Clear();
            mInitializedModels.Clear();
            mRegisteredUtilities.Clear();
            mSystems.Clear();
            mModels.Clear();
            
            mInited = false;
            if (ReferenceEquals(mArchitecture, this)) mArchitecture = null;
            
        }

        private void InitModel(IModel model)
        {
            model.Init();
            mInitializedModels.Add(model);
        }

        private void InitSystem(ISystem system)
        {
            system.Init();
            mInitializedSystems.Add(system);
        }

        private static void DeinitReverse<TItem>(List<TItem> items)
        {
            for (var i = items.Count-1; i>=0; i--)
            {
                if (items[i] is IDeinit deinitable)
                {
                    deinitable.Deinit();
                }
            }
        }
    }
}
