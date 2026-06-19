using System;

namespace Hoshino
{
    public enum SkillSerializedTypeKind : byte
    {
        Group = 0,
        Track = 1,
        Clip = 2,
        SpecialData = 3
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SkillGroupTypeAttribute : Attribute
    {
        public SkillGroupTypeAttribute(uint id)
        {
            Id = id;
        }

        public uint Id { get; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SkillTrackTypeAttribute : Attribute
    {
        public SkillTrackTypeAttribute(uint id)
        {
            Id = id;
        }

        public uint Id { get; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SkillClipTypeAttribute : Attribute
    {
        public SkillClipTypeAttribute(uint id)
        {
            Id = id;
        }

        public uint Id { get; }
    }

    /// <summary>
    /// 标记一个技能特殊数据类型，供序列化代码生成器识别。
    /// 挂在编辑态 C# class 上，字段用 <see cref="SkillCustomDataAttribute"/> 标记。
    /// 运行时生成 <c>RuntimeXxxData</c> struct + Blob 读写。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SkillSpecialDataTypeAttribute : Attribute
    {
        public SkillSpecialDataTypeAttribute(uint id)
        {
            Id = id;
        }

        public uint Id { get; }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class SkillExternalTypeAttribute : Attribute
    {
        public SkillExternalTypeAttribute(uint id, Type type, SkillSerializedTypeKind kind)
        {
            Id = id;
            Type = type;
            Kind = kind;
        }

        public uint Id { get; }
        public Type Type { get; }
        public SkillSerializedTypeKind Kind { get; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SkillCustomDataAttribute : Attribute
    {
    }
}
