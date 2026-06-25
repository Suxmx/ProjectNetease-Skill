using Hoshino.Skill.Executor;
using Hoshino.Skill.Executor.Samples;
using UnityEngine;

namespace Hoshino.Skill.Samples
{
    [SkillExecutor(SkillGeneratedIds.SingleDamageClip)]
    public class SingleDamageExecutor : LifecycleSkillNodeExecutor<SampleSkillContext, SingleDamageNodeData>
    {
        protected override void OnStart(in SampleSkillContext context, in SingleDamageNodeData data)
        {
            Debug.Log($"[SingleDamageExecutor] OnStart: {JsonUtility.ToJson(data)}");
        }

        protected override void OnTick(in SampleSkillContext context, in SingleDamageNodeData data)
        {
            Debug.Log($"[SingleDamageExecutor] OnTick: {JsonUtility.ToJson(data)}");
        }

        protected override void OnEnd(in SampleSkillContext context, in SingleDamageNodeData data)
        {
            Debug.Log($"[SingleDamageExecutor] OnEnd: {JsonUtility.ToJson(data)}");
        }
    }
}