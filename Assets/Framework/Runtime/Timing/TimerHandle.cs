namespace GameFramework
{
    /// <summary>
    /// 创建任务时返回， 之后可用于取消任务，调用方无法修改Id
    /// </summary>
    public readonly struct  TimerHandle
    {
        internal TimerHandle(int id)
        {
            Id = id;
        }
        internal int Id{ get; }
        public bool IsValid => Id > 0;//0表示没有有效任务
    }
}