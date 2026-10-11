using GameFramework;
using UnityEngine;

namespace GameFramework.Examples
{
    /// <summary>
    /// 将场景中的框架 UGUIRoot 交给示例 Architecture 注册的 UI 服务。
    /// </summary>
    public sealed class UITextEffectsDemoBootstrap : MonoBehaviour, IController
    {
        private void Awake()
        {
            var root = FindObjectOfType<UGUIRoot>();
            if (root == null)
            {
                Debug.LogError("文字效果示例场景缺少 UGUIRoot。", this);
                return;
            }

            this.GetUtility<IUGUIService>().BindRoot(root);
        }

        IArchitecture IBelongArchitecture.GetArchitecture()
        {
            return ExampleArchitecture.Interface;
        }
    }
}
