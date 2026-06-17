using Slate;
using UnityEngine;

namespace Hoshino
{
    [Slate.Icon(typeof(Transform))]
    [Attachable(typeof(ActorGroup))]
    [SkillTrackType(101u)]
    public class SkillActionTrack : CutsceneTrack
    {
        public SkillActionTrack()
        {
            color = new Color(0.19f, 0.54f, 0.92f);
        }
    }
}
