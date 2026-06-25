using Hoshino;

namespace Hoshino.Skill.Executor
{
    /// <summary>
    /// 技能节点执行上下文的最小契约。
    /// Executor 根基类只需这三项即可完成节点数据预加载读取与生命周期分发；
    /// 项目层自定义的 context 结构体实现本接口，并可自由附加业务字段（Player/Motor/网络状态等），
    /// 使 Executor 框架不依赖任何具体项目类型。
    /// </summary>
    public interface ISkillExecutionContext
    {
        /// <summary>当前技能定义，用于读取预加载的节点数据容器。</summary>
        SkillDefinition Skill { get; }

        /// <summary>当前正在执行的节点，提供 NodeId/ClipId/StartTick/EndTick。</summary>
        SkillRuntimeNode Node { get; }

        /// <summary>当前生命周期阶段，由项目调度器填入，用于分发到 OnStart/OnTick/OnEnd。</summary>
        ESkillNodeLifecyclePhase LifecyclePhase { get; }
    }
}
