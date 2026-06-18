using UnityEngine;

namespace Hoshino
{
    public static class SkillTickUtility
    {
        public const int DefaultTickRate = 60;

        public static int SecondsToTicks(float seconds)
        {
            return Mathf.Max(0, Mathf.RoundToInt(seconds * DefaultTickRate));
        }
    }
}
