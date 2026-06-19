#if UNITY_EDITOR
using System.Collections.Generic;
using Slate;

namespace Hoshino
{
    /// <summary>
    /// 技能数据黑板的内存缓存。按 Cutscene 实例存储 <see cref="SpecialDataEntry"/> 列表，
    /// 供 <see cref="SkillSerializer"/> 保存/加载和 <see cref="SkillBlackboardWindow"/> 编辑共用。
    /// Cutscene 本身不持有 specialDatas（避免改 Slate 核心类），由本缓存代理。
    /// </summary>
    public static class SkillBlackboardCache
    {
        private static readonly Dictionary<Cutscene, List<SpecialDataEntry>> s_Cache = new();

        /// <summary>获取指定 cutscene 的 specialDatas 列表（不存在则创建空列表）。</summary>
        public static List<SpecialDataEntry> Get(Cutscene cutscene)
        {
            if (cutscene == null)
                return new();

            if (!s_Cache.TryGetValue(cutscene, out List<SpecialDataEntry> list))
            {
                list = new();
                s_Cache[cutscene] = list;
            }
            return list;
        }

        /// <summary>设置指定 cutscene 的 specialDatas 列表（导入时用）。</summary>
        public static void Set(Cutscene cutscene, List<SpecialDataEntry> list)
        {
            if (cutscene == null)
                return;
            s_Cache[cutscene] = list ?? new();
        }

        /// <summary>清除指定 cutscene 的缓存（关闭文件时调）。</summary>
        public static void Clear(Cutscene cutscene)
        {
            if (cutscene != null)
                s_Cache.Remove(cutscene);
        }
    }
}
#endif
