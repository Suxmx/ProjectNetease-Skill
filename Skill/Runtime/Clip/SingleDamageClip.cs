using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    /// <summary>
    /// 单次伤害判定 Clip。无长度（固定 1 tick），仅在起始时刻执行一次伤害判定。
    /// 编辑器预览：OnRawUpdate 持续绘制判定范围，暂停时也可见。
    /// </summary>
    [SkillClipType(1007u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class SingleDamageClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 0.0167f;
        [SkillCustomData, LabelText("Shape")] public SkillHitShape Shape = SkillHitShape.Box;
        [SkillCustomData, LabelText("Space")] public SkillSpace Space = SkillSpace.AimDirection;
        [SkillCustomData, LabelText("Offset")] public Vector3 Offset = new(0f, 0.6f, 1.2f);
        [SkillCustomData, LabelText("Half Extents")] public Vector3 HalfExtents = new(0.8f, 0.6f, 0.9f);
        [SkillCustomData, LabelText("Radius")] public float Radius = 0.6f;
        [SkillCustomData, LabelText("Distance")] public float Distance = 4f;
        [SkillCustomData, LabelText("Hit Mask")] public LayerMask HitMask = ~0;
        [SkillCustomData, LabelText("Damage")] public int Damage = 10;
        [SkillCustomData, LabelText("Damage Group")] public byte DamageGroupId = 0;

        /// <summary>长度固定为 1 tick（约 0.0167s），不可调节。</summary>
        public override float length
        {
            get { return _length; }
            set { _length = 0.0167f; }
        }

        public override bool isValid => true;

        protected override void OnRawUpdate()
        {
            if (cutscene.currentTime < startTime || cutscene.currentTime > endTime)
                return;

            DrawDamagePreview();
        }

        /// <summary>绘制判定范围预览。</summary>
        private void DrawDamagePreview()
        {
            Vector3 center = ResolveWorldPosition(Offset);
            Quaternion rotation = ResolveWorldRotation();
            Color color = new(1f, 0.82f, 0.1f, 0.35f);

            if (Shape == SkillHitShape.Sphere)
                SkillDraw.SphereWithOutline(center, Mathf.Max(0.01f, Radius), color);
            else if (Shape == SkillHitShape.Box)
                SkillDraw.BoxWithOutline(center, rotation, Vector3.Max(HalfExtents, Vector3.one * 0.01f), color);
            else
                SkillDraw.SphereWithOutline(center, 0.1f, color);
        }

#if UNITY_EDITOR
        protected override void OnClipGUI(Rect rect)
        {
            float x = 0f;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            GUI.DrawTexture(new Rect(x, 0, 1.5f, rect.height), Slate.Styles.whiteTexture);
            GUI.color = prev;
        }
#endif

        /// <summary>根据 Space 将局部 Offset 转换到世界坐标。</summary>
        private Vector3 ResolveWorldPosition(Vector3 localOffset)
        {
            Transform actorTransform = actor != null ? actor.transform : null;
            if (actorTransform == null)
                return localOffset;

            return SkillPreviewUtility.ResolveVector(Space, localOffset, actorTransform, actorTransform.forward, false) + actorTransform.position;
        }

        /// <summary>根据 Space 解析世界旋转。</summary>
        private Quaternion ResolveWorldRotation()
        {
            Transform actorTransform = actor != null ? actor.transform : null;
            if (actorTransform == null)
                return Quaternion.identity;

            return SkillPreviewUtility.ResolveRotation(Space, actorTransform, actorTransform.forward);
        }
    }
}
