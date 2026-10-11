using System;
using System.Collections.Generic;
using Framework.Runtime.UI;

namespace GameFramework
{
    public class UINavigationService:IUINavigationService,IDeinit
    {
        private readonly IUGUIService mUGUIService;
        //底部到顶部保存当前流程中的页面实例
        private readonly List<UIPage> mPages = new List<UIPage>();
        //不处理连续点击, 加载期间拒绝下一次导航请求
        private bool mIsNavigating;

        public UINavigationService(IUGUIService uGUIService)
        {
            mUGUIService = uGUIService?? throw new ArgumentNullException(nameof(uGUIService));
            mUGUIService.pageClosed+= OnPageClosed;
        }
        public void PushAsync<T>(Action<T> onOpened, Action<string> onFailed = null) where T : UIPage
        {
            PushInternal<T>(null,onOpened, onFailed);
        }

        public void PushAsync<T, TData>(TData data, Action<T> onOpened, Action<string> onFailed = null) where T : UIPage, IUIPageDataReceiver<TData> where TData : class, IUIData
        {
            if (data==null)
            {
                onFailed?.Invoke("data is null");
                return;
            }

            PushInternal<T>(page => page.SetData(data), onOpened, onFailed);
        }

        private void PushInternal<T>(Action<T> applyData, Action<T> onOpened, Action<string> onFailed) where T : UIPage
        {
            if (mIsNavigating)
            {
                onFailed?.Invoke("Navigation is already in progress");
                return;
            } 
            mIsNavigating = true;
            mUGUIService.CreateAsync<T>(page =>
            {
                mIsNavigating = false;
                var existingIndex = mPages.IndexOf(page);
                // 索引 0 也是已经存在的页面，必须使用 >= 0。
                if (existingIndex >= 0)
                {
                    while (mPages.Count-1>existingIndex)
                    {
                        CloseTopPage();
                    }
                    applyData?.Invoke(page);
                    mUGUIService.Show(page);
                    onOpened?.Invoke(page);
                    return;


                }
                //新页面先得到数据 再进入 返回栈并显示
                if (mPages.Count>0)
                {   var previousPage = mPages[mPages.Count - 1];
                    previousPage.OnPause();
                    previousPage.SetInteractable(false);
                    mUGUIService.Hide(previousPage);
                }
                applyData?.Invoke(page);
                mPages.Add(page);
                mUGUIService.Show(page);
                page.SetInteractable(true);
                page.OnResume();
                onOpened?.Invoke(page);
            }, reason =>
            {
                mIsNavigating = false;
                onFailed?.Invoke(reason);
            });
        }

        

        public bool Back()
        {
            if (mIsNavigating || mPages.Count <= 1) return false;
            CloseTopPage();
            var previousPage = mPages[mPages.Count - 1];
            mUGUIService.Show(previousPage);
            previousPage.SetInteractable(true);
            previousPage.OnResume();
            return true;
        }

        public bool BackTo<T>() where T : UIPage
        {
            if (mIsNavigating) return false;
     
            var targetIndex = -1;
            for ( int i = mPages.Count-1;i>=0; i--)
            {
                if (mPages[i] is T)
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex<0)
            {
                return false;
            }

            while (mPages.Count-1>targetIndex)
            {
                CloseTopPage();
            }
            var targetPage = mPages[targetIndex];
            mUGUIService.Show(targetPage);
            targetPage.SetInteractable(true);
            targetPage.OnResume();
            return true;
        }
        

        public void Deinit()
        {
            mUGUIService.pageClosed -= OnPageClosed;
            mPages.Clear();
        }

        void CloseTopPage()
        {
            var lastIndex = mPages.Count - 1;
            var page = mPages[lastIndex];
            //先移除历史记录 再触发Close 避免PageClosed重复进行处理
            mPages.RemoveAt(lastIndex);
            page.SetInteractable(false);
            mUGUIService.Close(page);
        }

        private void OnPageClosed(UIPage page)
        {
            mPages.Remove(page);
        }

        
    }
    
    
    
}
