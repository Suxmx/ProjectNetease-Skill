using Drawing;
using UnityEngine;

namespace Hoshino
{
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

        public static void SphereWithOutline(Vector3 center, float radius, Color color)
        {
            Draw.ingame.WireSphere(center, radius, Color.black);
            SolidSphere(center, radius, color);
        }

        public static void SolidSphere(Vector3 center, float radius, Color color)
        {
            var matrix = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * (radius * 2));
            using (Draw.ingame.WithMatrix(matrix))
            {
                Draw.ingame.SolidMesh(SphereMesh, color);
            }
        }
        
    }
}