using System;

namespace Hoshino
{
    public enum SkillSerializedTypeKind : byte
    {
        Group = 0,
        Track = 1,
        Clip = 2
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
