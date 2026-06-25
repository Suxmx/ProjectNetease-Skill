using UnityEngine;

namespace Hoshino.Skill.Executor
{
    /// <summary>
    /// 带节点数据类型的 Executor 泛型根基类。实现 <see cref="ISkillNodeExecutor{TContext}"/>，
    /// 自动从 <see cref="SkillDefinition.PreloadedNodeData"/> 强类型读取节点数据，交由子类的 <see cref="OnExecute"/> 处理。
    /// 热路径仅一次接口转型 + 一次强类型字典查找，无反序列化、无装箱/拆箱。
    /// 不直接继承此类——继承 <see cref="LifecycleSkillNodeExecutor{TContext,TData}"/> 走默认生命周期分发，
    /// 或自定义基类实现自己的生命周期/分发策略。
    /// </summary>
    /// <typeparam name="TContext">项目自定义执行上下文结构体。</typeparam>
    /// <typeparam name="TData">节点数据结构体（生成的 XxxNodeData）。</typeparam>
    public abstract class SkillNodeExecutor<TContext, TData> : ISkillNodeExecutor<TContext>
        where TContext : struct, ISkillExecutionContext
        where TData : struct
    {
        /// <summary>
        /// 读取预加载节点数据并分发。由调度器每 tick 调用。
        /// </summary>
        public void Execute(in TContext context)
        {
            // --- 热路径：接口转型取强类型容器，无装箱；字典查找无拆箱 ---
            if (context.Skill != null
                && context.Skill.PreloadedNodeData is ISkillPreloadedNodeData<TData> preloaded
                && preloaded.TryGetValue(context.Node.NodeId, out TData data))
            {
                OnExecute(context, in data);
                return;
            }

            Debug.LogWarning($"[SkillExecutor] Node data missing or type mismatch. NodeId={context.Node.NodeId} ClipId={context.Node.ClipId} Expected={typeof(TData).Name}.");
        }

        /// <summary>子类实现具体节点逻辑。生命周期分发由更具体的基类（如 LifecycleSkillNodeExecutor）处理。</summary>
        protected abstract void OnExecute(in TContext context, in TData data);
    }
}
