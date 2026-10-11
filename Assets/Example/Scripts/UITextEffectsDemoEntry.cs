using GameFramework;
using UnityEngine;

namespace GameFramework.Examples
{
    /// <summary>
    /// 通过框架 UI 服务打开文字效果演示页。
    /// </summary>
    public sealed class UITextEffectsDemoEntry : MonoBehaviour, IController
    {
        private void Start()
        {
            this.GetUtility<IUGUIService>().OpenAsync<UITextEffectsDemoPage>(
                page => Debug.Log("文字效果演示页已打开: " + page.name),
                reason => Debug.LogError("文字效果演示页打开失败: " + reason));
        }

        IArchitecture IBelongArchitecture.GetArchitecture()
        {
            return ExampleArchitecture.Interface;
        }
    }
}
