using System.Collections.Generic;
using Drawing;
using UnityEngine;

namespace Hoshino
{
    /// <summary>
    /// 技能调试绘制工具。提供命中判定范围、血条、技能 CD 等调试可视化。
    /// 命中判定采用请求-淡出模式（提交一次，0.5s 内淡出消失），
    /// 血条/CD 由外部每帧调用持续绘制。
    /// </summary>
    public static class SkillDraw
    {
        #region Mesh

        private static Mesh CreateMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
            return mesh;
        }

        private static Mesh SphereMesh
        {
            get
            {
                if (s_SphereMesh == null)
                {
                    s_SphereMesh = CreateMesh(PrimitiveType.Sphere);
                }

                return s_SphereMesh;
            }
        }
        private static Mesh s_SphereMesh;

        #endregion

        #region 基础样式

        public static void SphereWithOutline(Vector3 center, float radius, Color color)
        {
            Draw.ingame.WireSphere(center, radius, Color.black);
            SolidSphere(center, radius, color);
        }

        public static void BoxWithOutline(Vector3 center, Quaternion rotation, Vector3 halfExtents, Color color)
        {
            Vector3 size = halfExtents * 2f;
            Draw.ingame.WireBox(center, rotation, size, Color.black);
            Draw.ingame.SolidBox(center, rotation, size, color);
        }

        public static void SolidSphere(Vector3 center, float radius, Color color)
        {
            var matrix = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * (radius * 2));
            using (Draw.ingame.WithMatrix(matrix))
            {
                Draw.ingame.SolidMesh(SphereMesh, color);
            }
        }

        #endregion

        #region 命中判定请求管理（0.5s 淡出）

        /// <summary>判定范围淡出持续时间（秒）。</summary>
        private const float FadeDuration = 0.5f;

        /// <summary>命中点标记半径。</summary>
        private const float HitMarkerRadius = 0.3f;

        private static readonly Color s_RangeColor = new(1f, 0.5f, 0f, 0.4f);
        private static readonly Color s_HitColor = Color.red;

        /// <summary>判定形状种类。</summary>
        private enum EShapeType
        {
            Box,
            Sphere,
            Ray
        }

        /// <summary>一次判定查询的绘制请求。</summary>
        private class Request
        {
            public EShapeType Shape;
            public Vector3 Center;
            public Quaternion Rotation;
            public Vector3 Size;
            public float Radius;
            public Vector3 Origin;
            public Vector3 End;
            public List<Vector3> HitPoints;
            public float StartTime;
            public float FadeDuration;
        }

        private static readonly List<Request> s_Requests = new();

        /// <summary>提交盒形判定绘制请求。</summary>
        public static void DrawHitBox(Vector3 center, Quaternion rotation, Vector3 halfExtents, List<Vector3> hitPoints, float fadeDuration = 0.5f)
        {
            s_Requests.Add(new Request
            {
                Shape = EShapeType.Box,
                Center = center,
                Rotation = rotation,
                Size = halfExtents * 2f,
                HitPoints = hitPoints,
                StartTime = Time.realtimeSinceStartup,
                FadeDuration = fadeDuration
            });
        }

        /// <summary>提交球形判定绘制请求。</summary>
        public static void DrawHitSphere(Vector3 center, float radius, List<Vector3> hitPoints, float fadeDuration = 0.5f)
        {
            s_Requests.Add(new Request
            {
                Shape = EShapeType.Sphere,
                Center = center,
                Radius = radius,
                HitPoints = hitPoints,
                StartTime = Time.realtimeSinceStartup,
                FadeDuration = fadeDuration
            });
        }

        /// <summary>提交射线判定绘制请求。</summary>
        public static void DrawHitRay(Vector3 origin, Vector3 direction, float distance, Vector3 hitPoint, bool hit, float fadeDuration = 0.5f)
        {
            List<Vector3> hitPoints = hit ? new List<Vector3> { hitPoint } : null;
            s_Requests.Add(new Request
            {
                Shape = EShapeType.Ray,
                Origin = origin,
                End = origin + direction * distance,
                HitPoints = hitPoints,
                StartTime = Time.realtimeSinceStartup,
                FadeDuration = fadeDuration
            });
        }

        /// <summary>每帧重绘未过期请求并淡出，由驱动调用。用 realtimeSinceStartup 适配编辑器非 playmode。</summary>
        public static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = s_Requests.Count - 1; i >= 0; i--)
            {
                Request req = s_Requests[i];
                float elapsed = now - req.StartTime;
                float fadeDur = req.FadeDuration > 0f ? req.FadeDuration : FadeDuration;
                if (elapsed >= fadeDur)
                {
                    s_Requests.RemoveAt(i);
                    continue;
                }

                float alpha = 1f - (elapsed / fadeDur);
                DrawRequest(req, alpha);
            }
        }

        /// <summary>按形状绘制判定范围与命中点，alpha 随时间淡出。</summary>
        private static void DrawRequest(Request req, float alpha)
        {
            Color rangeColor = s_RangeColor;
            rangeColor.a *= alpha;
            Color hitColor = s_HitColor;
            hitColor.a = alpha;

            // --- 绘制判定范围（带 outline 样式）---
            switch (req.Shape)
            {
                case EShapeType.Box:
                    BoxWithOutline(req.Center, req.Rotation, req.Size * 0.5f, rangeColor);
                    break;
                case EShapeType.Sphere:
                    SphereWithOutline(req.Center, req.Radius, rangeColor);
                    break;
                case EShapeType.Ray:
                    using (Draw.ingame.WithColor(rangeColor))
                    {
                        Draw.ingame.Arrow(req.Origin, req.End);
                    }
                    break;
            }

            // --- 绘制命中点标记 ---
            if (req.HitPoints == null || req.HitPoints.Count == 0)
                return;

            foreach (Vector3 p in req.HitPoints)
            {
                SphereWithOutline(p, HitMarkerRadius, hitColor);
            }
        }

        #endregion

        #region 血条 / CD 条

        /// <summary>血条在角色头顶的偏移。</summary>
        private const float HealthLabelYOffset = 2.2f;

        /// <summary>CD 条在血条下方的偏移。</summary>
        private const float CooldownLabelYOffset = 1.9f;

        /// <summary>文字大小（像素）。</summary>
        private const float LabelSize = 14f;

        /// <summary>绘制角色头顶血量数字。</summary>
        /// <param name="smoothTransform">平滑后的 transform（TickSmoother 图形对象）。</param>
        /// <param name="current">当前血量。</param>
        /// <param name="max">最大血量。</param>
        public static void HealthBar(Transform smoothTransform, int current, int max)
        {
            Vector3 pos = smoothTransform.position + Vector3.up * HealthLabelYOffset;
            Color color = Color.Lerp(Color.red, Color.green, max > 0 ? Mathf.Clamp01((float)current / max) : 0f);
            Draw.Label2D(pos, $"{current}/{max}", LabelSize, LabelAlignment.Center, color);
        }

        /// <summary>绘制角色头顶技能 CD 进度数字。</summary>
        /// <param name="smoothTransform">平滑后的 transform。</param>
        /// <param name="currentTick">当前已过 tick。</param>
        /// <param name="totalTicks">技能总 tick。</param>
        public static void CooldownBar(Transform smoothTransform, int currentTick, int totalTicks)
        {
            Vector3 pos = smoothTransform.position + Vector3.up * CooldownLabelYOffset;
            Draw.Label2D(pos, $"{currentTick}/{totalTicks}", LabelSize, LabelAlignment.Center, Color.cyan);
        }

        #endregion
    }
}
