using System;

namespace Hoshino.Skill.Executor
{
    /// <summary>
    /// 标记一个类为技能节点 Executor，绑定 ClipId。
    /// domain 由该类继承的、带 <see cref="SkillExecutorDomainAttribute"/> 的基类决定，
    /// 代码生成器扫描此特性 + 继承链生成 <c>SkillGeneratedExecutorMetas</c>。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SkillExecutorAttribute : Attribute
    {
        public SkillExecutorAttribute(uint clipId)
        {
            ClipId = clipId;
        }

        public uint ClipId { get; }
    }
}
