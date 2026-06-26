using Hoshino.Skill.Executor;
using Hoshino.Skill.Executor.Samples;
using UnityEngine;

namespace Hoshino.Skill.Samples
{
    [SkillExecutor(SkillGeneratedIds.MultiDamageClip)]
    public class MultiDamageExecutor : LifecycleSkillNodeExecutor<SampleSkillContext, MultiDamageNodeData>
    {
        protected override void OnStart(in SampleSkillContext context, in MultiDamageNodeData data)
        {
            Debug.Log($"[SingleDamageExecutor] OnStart: {JsonUtility.ToJson(data)}");
        }

        protected override void OnTick(in SampleSkillContext context, in MultiDamageNodeData data)
        {
            Debug.Log($"[SingleDamageExecutor] OnTick: {JsonUtility.ToJson(data)}");
        }

        protected override void OnEnd(in SampleSkillContext context, in MultiDamageNodeData data)
        {
            Debug.Log($"[SingleDamageExecutor] OnEnd: {JsonUtility.ToJson(data)}");
        }
    }
}