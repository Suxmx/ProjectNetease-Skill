using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    /// <summary>
    /// 多次伤害判定 Clip。有长度，按 HitIntervalTicks 间隔执行多次伤害判定。
    /// 编辑器预览：仅在判定 tick 附近（±2 tick 容差）通过 OnRawUpdate 持续绘制判定范围，
    /// 非判定帧不绘制。Odin 显示总判定次数、预计 DPS、当前 tick。
    /// </summary>
    [SkillClipType(1008u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class MultiDamageClip : ActionClip
    {

        [SerializeField, HideInInspector] private float _length = 1f;
        [SkillCustomData, LabelText("Shape")] public SkillHitShape Shape = SkillHitShape.Box;
        [SkillCustomData, LabelText("Space")] public SkillSpace Space = SkillSpace.AimDirection;
        [SkillCustomData, LabelText("Offset")] public Vector3 Offset = new(0f, 0.6f, 1.2f);
        [SkillCustomData, LabelText("Half Extents")] public Vector3 HalfExtents = new(0.8f, 0.6f, 0.9f);
        [SkillCustomData, LabelText("Radius")] public float Radius = 0.6f;
        [SkillCustomData, LabelText("Distance")] public float Distance = 4f;
        [SkillCustomData, LabelText("Hit Mask")] public LayerMask HitMask = ~0;
        [SkillCustomData, LabelText("Damage")] public int Damage = 10;
        [SkillCustomData, LabelText("Damage Group")] public byte DamageGroupId = 0;
        [SkillCustomData, LabelText("Hit Interval (ticks)")] public byte HitIntervalTicks = 10;

        public override float length
        {
            get { return _length; }
            set { _length = value; }
        }

        public override bool isValid => true;

        /// <summary>总判定次数（含起始时刻）。</summary>
        public int HitCount
        {
            get
            {
                int lengthTicks = SkillTickUtility.SecondsToTicks(length, 60);
                if (HitIntervalTicks <= 0)
                    return 1;
                return lengthTicks / HitIntervalTicks + 1;
            }
        }

        /// <summary>预计 DPS（伤害 × 次数 / 持续秒数）。</summary>
        public float EstimatedDPS
        {
            get
            {
                if (length <= 0f)
                    return 0f;
                return Damage * HitCount / length;
            }
        }

        /// <summary>OnRawUpdate：仅在判定 tick 附近（±容差）绘制判定范围。</summary>
        protected override void OnRawUpdate()
        {
            if (cutscene.currentTime < startTime || cutscene.currentTime > endTime)
                return;

            int elapsedTick = SkillTickUtility.SecondsToTicks(cutscene.currentTime - startTime, 60);
            if (!IsHitTick(elapsedTick))
                return;

            DrawDamagePreview();
        }

        /// <summary>判断当前 tick 是否在任一判定 tick 的可见范围内。
        /// 可见范围 = interval 的一半，确保在下一个判定帧之前消失。</summary>
        private bool IsHitTick(int elapsedTick)
        {
            if (elapsedTick < 0)
                return false;

            if (HitIntervalTicks <= 0)
                return elapsedTick == 0;

            int halfInterval = Mathf.Max(1, HitIntervalTicks / 2);
            for (int t = 0; t <= SkillTickUtility.SecondsToTicks(length, 60); t += HitIntervalTicks)
            {
                if (Mathf.Abs(elapsedTick - t) < halfInterval)
                    return true;
            }
            return false;
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
            int lengthTicks = SkillTickUtility.SecondsToTicks(length, 60);
            if (lengthTicks <= 0)
                return;

            if (HitIntervalTicks <= 0)
            {
                DrawTickLine(rect, 0f);
            }
            else
            {
                for (int t = 0; t <= lengthTicks; t += HitIntervalTicks)
                {
                    float normalizedX = (float)t / lengthTicks;
                    DrawTickLine(rect, normalizedX);
                }
            }
        }

        private void DrawTickLine(Rect rect, float normalizedX)
        {
            float x = normalizedX * rect.width;
            Color prev = GUI.color;
            GUI.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            GUI.DrawTexture(new Rect(x, 0, 1.5f, rect.height), Slate.Styles.whiteTexture);
            GUI.color = prev;
        }

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("当前 Tick"), PropertyOrder(-1)]
        private int CurrentTickDisplay
        {
            get
            {
                if (cutscene == null)
                    return 0;
                return Mathf.FloorToInt(cutscene.currentTime * 60);
            }
        }

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("总判定次数"), PropertyOrder(-1)]
        private int HitCountDisplay => HitCount;

        [ShowInInspector, Sirenix.OdinInspector.ReadOnly, LabelText("预计 DPS"), PropertyOrder(-1)]
        private float DPSDisplay => EstimatedDPS;
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
