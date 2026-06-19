using UnityEngine;

namespace Hoshino
{
    /// <summary>
    /// 技能编辑器预览空间换算工具。将 Clip 数据中的局部向量/旋转
    /// 按 <see cref="SkillSpace"/> 转换到世界空间，供 Clip 的 OnRawUpdate/OnFixedTick 预览使用。
    /// 运行时由 <c>Battle.SkillUtility</c> 承担相同职责，此处为编辑器侧独立实现。
    /// </summary>
    public static class SkillPreviewUtility
    {
        /// <summary>根据空间模式解析旋转。</summary>
        public static Quaternion ResolveRotation(SkillSpace space, Transform actor, Vector3 aimDirection)
        {
            switch (space)
            {
                case SkillSpace.ActorForward:
                    return actor.rotation;
                case SkillSpace.AimDirection:
                    aimDirection.y = 0f;
                    if (aimDirection.sqrMagnitude <= 0.0001f)
                        aimDirection = actor.forward;
                    return Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
                default:
                    return Quaternion.identity;
            }
        }

        /// <summary>根据空间模式将局部向量转换到世界空间。</summary>
        /// <param name="flattenValue">是否将 Y 分量归零（俯视角 2D 模式）。</param>
        public static Vector3 ResolveVector(SkillSpace space, Vector3 value, Transform actor, Vector3 aimDirection, bool flattenValue = true)
        {
            if (flattenValue)
                value.y = 0f;

            switch (space)
            {
                case SkillSpace.ActorForward:
                    return actor.TransformDirection(value);
                case SkillSpace.AimDirection:
                    aimDirection.y = 0f;
                    if (aimDirection.sqrMagnitude <= 0.0001f)
                        aimDirection = actor.forward;
                    Quaternion aimRotation = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
                    return aimRotation * value;
                default:
                    return value;
            }
        }
    }
}
