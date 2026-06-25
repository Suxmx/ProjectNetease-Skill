namespace Hoshino.Skill.Executor
{
    /// <summary>
    /// 节点生命周期阶段。由项目调度器根据节点 active 区间变化填入 context，
    /// <see cref="LifecycleSkillNodeExecutor{TContext,TData}"/> 据此分发到 OnStart/OnTick/OnEnd。
    /// 项目可自定义更多阶段并派生自己的基类，本枚举仅提供默认三段式。
    /// </summary>
    public enum ESkillNodeLifecyclePhase : byte
    {
        Start = 0,
        Tick = 1,
        End = 2
    }
}
