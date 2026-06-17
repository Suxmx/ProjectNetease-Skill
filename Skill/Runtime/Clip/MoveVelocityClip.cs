using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    [SkillClipType(1001u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class MoveVelocityClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 0.2f;
        [SkillCustomData, LabelText("Space")] public SkillSpace Space = SkillSpace.AimDirection;
        [SkillCustomData, LabelText("Velocity")] public Vector3 Velocity;

        public override float length
        {
            get { return _length; }
            set { _length = value; }
        }

        public override bool isValid => true;

        protected override void OnRawUpdate()
        {
            if (cutscene.currentTime == 0 || cutscene.currentTime < startTime || cutscene.currentTime > endTime)
                return;

            SkillDraw.SphereWithOutline(Velocity * 0.1f, 0.12f, new Color(0.1f, 0.7f, 1f, 0.35f));
        }
    }
}
