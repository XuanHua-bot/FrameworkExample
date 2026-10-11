using GameFramework;

namespace Framework.Runtime.UI
{
    /// <summary>
    /// 标记 可作为UI打开参数的数据类型
    /// 数据具体有哪些字段 由业务页面自己定义
    /// </summary>
    public interface IUIData
    {
        
    }
    /// <summary>
    /// 页面声明自己能接受哪一种打开参数
    /// TData 写在接口上 调用方传错类型 会直接在编译期 报错
    /// </summary>
    public interface IUIPageDataReceiver<T>
    {
        void SetData(T data);
    }
}