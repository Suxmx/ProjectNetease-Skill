// ============================================================================
// 此文件位于 Samples~ 目录，Unity 不会编译它（~ 后缀目录被忽略）。
// 作为「开箱即用」参考：复制本文件到你的项目 Assets 下（去掉 ~ 目录层级）即可使用。
// 演示非网络环境下 SkillEditor 产物 .bytes 的完整运行时调度链路：
//   加载 → tick 推进 → 节点 active 状态跟踪 → Start/Tick/End 分发 → Executor 执行
// !!! 使用前可能需要生成一遍Executor的绑定代码，否则会有报错 !!!!
// ============================================================================

using System.Collections.Generic;
using Hoshino;
using Hoshino.Skill.Executor;
using UnityEngine;

namespace Hoshino.Skill.Executor.Samples
{
    /// <summary>
    /// 最小执行上下文样例。实现 <see cref="ISkillExecutionContext"/> 三项必需字段，
    /// 并附加一个业务字段 <c>Actor</c> 供具体 Executor 使用。
    /// 项目可仿照此结构自定义自己的 context，加入 Player/Motor/网络状态等。
    /// </summary>
    public readonly struct SampleSkillContext : ISkillExecutionContext
    {
        public SkillDefinition Skill { get; }
        public SkillRuntimeNode Node { get; }
        public ESkillNodeLifecyclePhase LifecyclePhase { get; }
        public Transform Actor { get; }

        public SampleSkillContext(SkillDefinition skill, SkillRuntimeNode node, ESkillNodeLifecyclePhase phase, Transform actor)
        {
            Skill = skill;
            Node = node;
            LifecyclePhase = phase;
            Actor = actor;
        }
    }

    /// <summary>
    /// 最小技能运行时调度器样例。镜像 legacy SkillController 的单域调度逻辑（去除网络/replay）。
    /// 把编译产物 .bytes 拖到 Inspector，运行后按固定 tick 推进技能，分发节点生命周期。
    /// </summary>
    public class SkillRuntimePlayer : MonoBehaviour
    {
        [SerializeField] private TextAsset _skillBinary;
        [SerializeField] private float _tickRate = 60f;

        private SkillDefinition _skill;
        private float _tickAccumulator;
        private int _elapsedTicks;
        private bool _isActive;
        private readonly HashSet<int> _activeNodeIds = new();

        private void Awake()
        {
            if (_skillBinary != null)
                _skill = SkillDefinition.FromBytes(_skillBinary.bytes);
        }

        private void Update()
        {
            if (!_isActive || _skill == null)
                return;

            // --- 按固定 tick 推进 ---
            _tickAccumulator += Time.deltaTime;
            float tickDelta = 1f / Mathf.Max(1f, _tickRate);
            while (_tickAccumulator >= tickDelta)
            {
                _tickAccumulator -= tickDelta;
                Tick();
            }
        }

        [ContextMenu("Play")]
        public void Play()
        {
            if (_skill == null)
                return;
            _elapsedTicks = 0;
            _isActive = true;
            _activeNodeIds.Clear();
        }

        [ContextMenu("Stop")]
        public void Stop()
        {
            if (!_isActive)
                return;
            // --- 停止时对所有 active 节点触发 End ---
            StopAllActive();
            _isActive = false;
        }

        private void Tick()
        {
            if (_elapsedTicks > _skill.LengthTicks)
            {
                Stop();
                return;
            }

            SkillRuntimeNode[] nodes = _skill.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                // 这里按需求添加，示例项目对 Domain (或者说节点类型 如ServerOnly ClientOnly) 等没需求
                // if (!SkillGeneratedExecutorBindings.TryGetDomain(node.ClipId, out _))
                //     continue;

                int nodeId = node.NodeId;
                bool isActive = node.IsActiveAt(_elapsedTicks);
                bool wasActive = _activeNodeIds.Contains(nodeId);

                if (isActive && !wasActive)
                {
                    _activeNodeIds.Add(nodeId);
                    ExecuteNode(node, ESkillNodeLifecyclePhase.Start);
                    ExecuteNode(node, ESkillNodeLifecyclePhase.Tick);
                }
                else if (isActive && wasActive)
                {
                    ExecuteNode(node, ESkillNodeLifecyclePhase.Tick);
                }
                else if (!isActive && wasActive)
                {
                    ExecuteNode(node, ESkillNodeLifecyclePhase.Tick);
                    _activeNodeIds.Remove(nodeId);
                    ExecuteNode(node, ESkillNodeLifecyclePhase.End);
                }
            }

            _elapsedTicks++;
        }

        private void ExecuteNode(SkillRuntimeNode node, ESkillNodeLifecyclePhase phase)
        {
            if (!SkillGeneratedExecutorBindings.TryGetExecutor(node.ClipId, out ISkillNodeExecutor<SampleSkillContext> executor))
                return;

            var context = new SampleSkillContext(_skill, node, phase, transform);
            executor.Execute(context);
        }

        private void StopAllActive()
        {
            if (_skill == null)
                return;
            SkillRuntimeNode[] nodes = _skill.Nodes;
            for (int i = 0; i < nodes.Length; i++)
            {
                SkillRuntimeNode node = nodes[i];
                if (_activeNodeIds.Contains(node.NodeId))
                    ExecuteNode(node, ESkillNodeLifecyclePhase.End);
            }
            _activeNodeIds.Clear();
        }
    }
}

// ============================================================================
// 用户自定义 Executor 示例（放在项目里，非 Samples~）：
//
// using Hoshino;
// using Hoshino.Skill.Executor;
// using UnityEngine;
//
// namespace MyGame
// {
//     // 1) 自定义 domain 基类（继承默认生命周期基类 + 标记 domain 编号）
//     public enum EMyDomain { Gameplay = 1, Server = 2 }
//
//     [SkillExecutorDomain((int)EMyDomain.Gameplay)]
//     public abstract class GameplayExecutor<TData> : LifecycleSkillNodeExecutor<SampleSkillContext, TData>
//         where TData : struct
//     {
//     }
//
//     // 2) 具体 Executor：[SkillExecutor] 绑定 ClipId，OnTick 写逻辑
//     [SkillExecutor(SkillGeneratedIds.DashClip)]
//     public sealed class DashExecutor : GameplayExecutor<DashNodeData>
//     {
//         protected override void OnTick(in SampleSkillContext context, in DashNodeData data)
//         {
//             // context.Actor 是样例 context 提供的业务字段
//             Vector3 displacement = data.Direction * data.Speed * (1f / 60f);
//             context.Actor.position += displacement;
//         }
//     }
// }
// ============================================================================
