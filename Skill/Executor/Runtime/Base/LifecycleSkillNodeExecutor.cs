namespace Hoshino.Skill.Executor
{
    /// <summary>
    /// 默认三段式生命周期 Executor 基类。按 <see cref="ESkillNodeLifecyclePhase"/> 分发到
    /// <see cref="OnStart"/>/<see cref="OnTick"/>/<see cref="OnEnd"/>，子类按需 override。
    /// 项目自定义 domain 基类通常继承本类并打上 <see cref="SkillExecutorDomainAttribute"/>，
    /// 例如 <c>[SkillExecutorDomain("Gameplay")] abstract class GameplayExecutor&lt;TData&gt; : LifecycleSkillNodeExecutor&lt;MyContext, TData&gt;</c>。
    /// 若需不同生命周期，可自定义基类直接继承 <see cref="SkillNodeExecutor{TContext,TData}"/> 并走自己的分发。
    /// </summary>
    public abstract class LifecycleSkillNodeExecutor<TContext, TData>
        : SkillNodeExecutor<TContext, TData>
        where TContext : struct, ISkillExecutionContext
        where TData : struct
    {
        protected sealed override void OnExecute(in TContext context, in TData data)
        {
            switch (context.LifecyclePhase)
            {
                case ESkillNodeLifecyclePhase.Start:
                    OnStart(context, in data);
                    break;
                case ESkillNodeLifecyclePhase.Tick:
                    OnTick(context, in data);
                    break;
                case ESkillNodeLifecyclePhase.End:
                    OnEnd(context, in data);
                    break;
            }
        }

        /// <summary>节点进入 active 区间时调用一次。</summary>
        protected virtual void OnStart(in TContext context, in TData data) { }

        /// <summary>active 区间内每 tick 调用（含 StartTick）。</summary>
        protected virtual void OnTick(in TContext context, in TData data) { }

        /// <summary>节点离开 active 区间时调用一次。</summary>
        protected virtual void OnEnd(in TContext context, in TData data) { }
    }
}
