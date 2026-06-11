using Slate;
using UnityEngine;

namespace Hoshino
{
    [Slate.Icon(typeof(BoxCollider))]
    [Attachable(typeof(ActorGroup))]
    public class CollisionTrack : CutsceneTrack
    {
        public CollisionTrack()
        {
            color = new Color(0.2933586f, 0.71f, 0.1f);
        }
    }
}