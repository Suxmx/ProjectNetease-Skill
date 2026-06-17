using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    [SkillClipType(1004u)]
    [Attachable(typeof(SkillActionTrack), typeof(CollisionTrack))]
    public class CollisionClip : ActionClip, IMultiLineClip
    {
        [SerializeField, HideInInspector] private float _length = 3f;
        [SkillCustomData, LabelText("Shape")] public SkillHitShape Shape = SkillHitShape.Box;
        [SkillCustomData, LabelText("Space")] public SkillSpace Space = SkillSpace.AimDirection;
        [SkillCustomData, LabelText("Offset")] public Vector3 Offset = new(0f, 0.6f, 1.2f);
        [SkillCustomData, LabelText("Half Extents")] public Vector3 HalfExtents = new(0.8f, 0.6f, 0.9f);
        [SkillCustomData, LabelText("Radius")] public float Radius = 0.6f;
        [SkillCustomData, LabelText("Distance")] public float Distance = 4f;
        [SkillCustomData, LabelText("Hit Mask")] public LayerMask HitMask = ~0;
        [SkillCustomData, LabelText("Damage")] public int Damage = 10;

        public override float length
        {
            get { return _length; }
            set { _length = value; }
        }

        public override bool isValid => true;

        public void SetLine(int line)
        {
            _line = line;
        }
        
        protected override void OnRawUpdate()
        {
            if (cutscene.currentTime==0 || cutscene.currentTime < startTime || cutscene.currentTime > endTime)
            {
                return;
            }

            Color color = new(1f, 0.82f, 0.1f, 0.35f);
            if (Shape == SkillHitShape.Sphere)
                SkillDraw.SphereWithOutline(Offset, Mathf.Max(0.01f, Radius), color);
            else if (Shape == SkillHitShape.Box)
                SkillDraw.BoxWithOutline(Offset, Quaternion.identity, Vector3.Max(HalfExtents, Vector3.one * 0.01f), color);
        }
    }
}
