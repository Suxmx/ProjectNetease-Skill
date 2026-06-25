namespace Hoshino.Skill.Executor
{
    /// <summary>
    /// Executor 统一接口。项目调度器通过 <see cref="SkillExecutorRegistry{TContext}"/> 按 ClipId 取出实例并调用。
    /// 泛型参数约束为 <c>struct, ISkillExecutionContext</c>，保证结构体 context 经 <c>in</c> 传递无装箱。
    /// </summary>
    /// <typeparam name="TContext">项目自定义的执行上下文结构体。</typeparam>
    public interface ISkillNodeExecutor<TContext> where TContext : struct, ISkillExecutionContext
    {
        void Execute(in TContext context);
    }
}
