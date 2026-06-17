using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    [SkillClipType(1002u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class MoveDisplacementClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 0.2f;
        [SkillCustomData, LabelText("Space")] public SkillSpace Space = SkillSpace.AimDirection;
        [SkillCustomData, LabelText("Displacement Per Second")] public Vector3 DisplacementPerSecond;

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

            SkillDraw.SphereWithOutline(DisplacementPerSecond * 0.1f, 0.12f, new Color(0.1f, 1f, 0.6f, 0.35f));
        }
    }
}
