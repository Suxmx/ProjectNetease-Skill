namespace Hoshino.Skill.Executor
{
    /// <summary>
    /// 预加载节点数据的强类型读取契约。
    /// 生成器为每个 Clip 的 NodeData 生成一份实现：持有 <c>Dictionary&lt;int, TData&gt;</c>，
    /// 由 <c>SkillGeneratedNodeDataBlob.Preload</c> 在技能加载期一次性填充。
    /// Executor 根基类热路径经 <c>is ISkillPreloadedNodeData&lt;TData&gt;</c> 接口转型后调用
    /// <see cref="TryGetValue"/>，全程无反序列化、无装箱、无拆箱。
    /// </summary>
    /// <typeparam name="TData">节点数据结构体（生成的 XxxNodeData）。</typeparam>
    public interface ISkillPreloadedNodeData<TData> where TData : struct
    {
        /// <summary>按 NodeId 取出预加载的节点数据。</summary>
        bool TryGetValue(int nodeId, out TData data);
    }
}
