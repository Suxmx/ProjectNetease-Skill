using System;

namespace Hoshino.Skill.Executor
{
    /// <summary>
    /// 标记一个 Executor domain 基类，声明其 domain 编号。
    /// 代码生成器遍历 Executor 类型的基类继承链，发现带此特性的基类（含泛型定义）即取其编号写入生成表，
    /// 从而使 domain 可由项目自由扩展，无需修改框架枚举或生成器。
    /// domain 编号在生成表中存为 int，项目调度器自行映射回自己的枚举。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class SkillExecutorDomainAttribute : Attribute
    {
        public SkillExecutorDomainAttribute(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }
}
