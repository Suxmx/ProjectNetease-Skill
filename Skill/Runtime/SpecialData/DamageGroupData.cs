using Sirenix.OdinInspector;
using UnityEngine;

namespace Hoshino
{
    /// <summary>
    /// 伤害组特殊数据。定义一个伤害组的配置（组 ID + 每受击方最多命中次数）。
    /// 挂在技能级数据黑板，供 <see cref="CollisionClip"/> 通过 DamageGroupId 引用。
    /// 同组内多个 CollisionClip 共享命中次数限制。
    /// </summary>
    [SkillSpecialDataType(2001u)]
    public class DamageGroupData
    {
        [SkillCustomData, LabelText("Group Id")]
        [Tooltip("伤害组 ID，CollisionClip 通过此 ID 绑定到本组。")]
        public byte GroupId = 1;

        [SkillCustomData, LabelText("Max Hits/Target")]
        [Tooltip("该组内每个受击方最多被命中次数。")]
        public byte MaxHitsPerTarget = 1;
    }
}
