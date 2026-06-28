#if UNITY_EDITOR
using System.Collections.Generic;
using Slate;

namespace Hoshino
{
    /// <summary>
    /// 技能级 Actor 绑定的内存缓存。按 Cutscene 实例存储预制体/FBX 的 AssetDatabase 路径，
    /// 供 <see cref="SkillSerializer"/> 保存/加载和 <see cref="SkillEditor"/> 预览共用。
    /// Cutscene 本身不持有 characterReference（避免改 Slate 核心类），由本缓存代理。
    /// </summary>
    public static class SkillActorBindingCache
    {
        private static readonly Dictionary<Cutscene, string> s_Cache = new();

        /// <summary>获取指定 cutscene 的 Actor 绑定路径（未设置返回空串）。</summary>
        public static string Get(Cutscene cutscene)
        {
            if (cutscene == null)
                return string.Empty;
            return s_Cache.TryGetValue(cutscene, out string path) ? (path ?? string.Empty) : string.Empty;
        }

        /// <summary>设置指定 cutscene 的 Actor 绑定路径（导入或用户选择时调）。</summary>
        public static void Set(Cutscene cutscene, string path)
        {
            if (cutscene == null)
                return;
            s_Cache[cutscene] = path ?? string.Empty;
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
