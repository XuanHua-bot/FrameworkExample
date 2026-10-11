namespace GameFramework
{
    public interface IController : IBelongArchitecture, ICanGetUtility, ICanSendCommand,
        ICanGetSystem, ICanGetModel, ICanRegisterEvent, ICanSendQuery
    {
    }
    /// <summary>
    /// System 在创建外部资源，订阅外部回调或启动持续任务时  必须实现IDeinit
    /// </summary>
    public interface ISystem : IBelongArchitecture, ICanSetArchitecture,
        ICanGetModel, ICanGetUtility, ICanSendEvent, ICanRegisterEvent, ICanGetSystem
    {
        void Init();
    }

    public abstract class AbstractISystem : ISystem
    {
        private IArchitecture mArchitecture;

        IArchitecture IBelongArchitecture.GetArchitecture()
        {
            return mArchitecture;
        }

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture)
        {
            mArchitecture = architecture;
        }

        void ISystem.Init()
        {
            OnInit();
        }

        protected abstract void OnInit();
    }

    public interface IModel : IBelongArchitecture, ICanSetArchitecture,
        ICanGetUtility, ICanSendEvent
    {
        void Init();
    }

    public abstract class AbstractModel : IModel
    {
        private IArchitecture mArchitecture;

        IArchitecture IBelongArchitecture.GetArchitecture()
        {
            return mArchitecture;
        }

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture)
        {
            mArchitecture = architecture;
        }

        void IModel.Init()
        {
            OnInit();
        }

        protected abstract void OnInit();
    }

    public interface IUtility
    {
    }

    public interface IDeinit//只有需要清理时 才额外实现，比如注销xx事件，释放缓存等
    {
        void Deinit();
    }

    public interface ICommand : IBelongArchitecture, ICanSetArchitecture,
        ICanGetModel, ICanGetUtility, ICanGetSystem, ICanSendEvent,
        ICanSendCommand, ICanSendQuery
    {
        void Execute();
    }
    public interface ICommand<TResult> : IBelongArchitecture, ICanSetArchitecture,
        ICanGetModel, ICanGetUtility, ICanGetSystem, ICanSendEvent,
        ICanSendCommand, ICanSendQuery
    {
        TResult Execute();
    }

    public abstract class AbstractCommand : ICommand
    {
        private IArchitecture mArchitecture;

        IArchitecture IBelongArchitecture.GetArchitecture()
        {
            return mArchitecture;
        }

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture)
        {
            mArchitecture = architecture;
        }

        void ICommand.Execute()
        {
            OnExecute();
        }

        protected abstract void OnExecute();
    }
    public abstract class AbstractCommand<TResult> : ICommand<TResult>
    {
        private IArchitecture mArchitecture;

        IArchitecture IBelongArchitecture.GetArchitecture()
        {
            return mArchitecture;
        }

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture)
        {
            mArchitecture = architecture;
        }

        TResult ICommand<TResult>.Execute()
        {
            return OnExecute();
        }

        protected abstract TResult OnExecute();
    }

    public interface IQuery<TResult> : IBelongArchitecture, ICanSetArchitecture,
        ICanGetModel, ICanGetSystem, ICanSendQuery
    {
        TResult Do();
    }

    public abstract class AbstractQuery<T> : IQuery<T>
    {
        private IArchitecture mArchitecture;

        public T Do()
        {
            return OnDo();
        }

        protected abstract T OnDo();

        public IArchitecture GetArchitecture()
        {
            return mArchitecture;
        }

        public void SetArchitecture(IArchitecture architecture)
        {
            mArchitecture = architecture;
        }
    }
}
