using UnityEngine;

namespace Hoshino
{
    /// <summary>
    /// 技能 tick 时间换算工具。支持 30/60 两种 tick 率，
    /// 编译默认 60，编辑器 tick 模式可选 30/60。
    /// </summary>
    public static class SkillTickUtility
    {
        /// <summary>编译默认 tick 率（60/s）。</summary>
        public const int DefaultTickRate = 60;

        /// <summary>支持的 tick 率选项。</summary>
        public static readonly int[] SupportedTickRates = { 30, 60 };

        /// <summary>按指定 tick 率将秒换算为 tick 数。</summary>
        public static int SecondsToTicks(float seconds, int tickRate = DefaultTickRate)
        {
            return Mathf.Max(0, Mathf.RoundToInt(seconds * tickRate));
        }

        /// <summary>按指定 tick 率将 tick 数换算为秒。</summary>
        public static float TicksToSeconds(int ticks, int tickRate = DefaultTickRate)
        {
            return (float)ticks / Mathf.Max(1, tickRate);
        }
    }
}
