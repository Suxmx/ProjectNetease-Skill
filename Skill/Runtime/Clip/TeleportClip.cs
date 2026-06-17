using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    [SkillClipType(1003u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class TeleportClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 0.1f;
        [SkillCustomData, LabelText("Space")] public SkillSpace Space = SkillSpace.World;
        [SkillCustomData, LabelText("Offset")] public Vector3 Offset;
        [SkillCustomData, LabelText("Use Command Target Point")] public bool UseCommandTargetPoint;

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

            SkillDraw.SphereWithOutline(Offset, 0.35f, new Color(0.7f, 0.45f, 1f, 0.35f));
        }
    }
}
