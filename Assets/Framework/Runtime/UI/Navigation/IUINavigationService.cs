using System;
using Framework.Runtime.UI;

namespace GameFramework
{
    public interface IUINavigationService:IUtility
    {
        void PushAsync<T>(Action<T> onOpened, Action<string> onFailed = null) where T : UIPage;
        void PushAsync<T,TData>(TData data,Action<T>onOpened,Action<string>onFailed=null) 
            where T:UIPage,IUIPageDataReceiver<TData>
            where TData:class,IUIData;
        bool Back();
        bool BackTo<T>() where T : UIPage;
    }
}