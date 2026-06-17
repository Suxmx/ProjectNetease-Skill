using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    [SkillClipType(1006u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class AttributeModifierClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 0.1f;
        [SkillCustomData, LabelText("Attribute Key")] public string AttributeKey;
        [SkillCustomData, LabelText("Add Value")] public float AddValue;
        [SkillCustomData, LabelText("Multiply Value")] public float MultiplyValue = 1f;
        [SkillCustomData, LabelText("Duration Seconds")] public float DurationSeconds;

        public override float length
        {
            get { return _length; }
            set { _length = value; }
        }

        public override bool isValid => true;
    }
}
