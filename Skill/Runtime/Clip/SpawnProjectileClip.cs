using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    [SkillClipType(1005u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class SpawnProjectileClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 0.1f;
        [SkillCustomData, LabelText("Projectile Id")] public int ProjectileId;
        [SkillCustomData, LabelText("Speed")] public float Speed = 10f;
        [SkillCustomData, LabelText("Lifetime Seconds")] public float LifetimeSeconds = 2f;
        [SkillCustomData, LabelText("Direction Space")] public SkillSpace DirectionSpace = SkillSpace.AimDirection;
        [SkillCustomData, LabelText("Spawn Offset")] public Vector3 SpawnOffset;
        [SkillCustomData, LabelText("Damage")] public int Damage = 10;

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

            SkillDraw.SphereWithOutline(SpawnOffset, 0.2f, new Color(1f, 0.42f, 0.15f, 0.35f));
        }
    }
}
