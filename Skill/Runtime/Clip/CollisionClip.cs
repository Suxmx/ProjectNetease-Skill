using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    [Attachable(typeof(CollisionTrack))]
    public class CollisionClip : ActionClip, IMultiLineClip
    {
        [SerializeField, HideInInspector] private float _length = 3f;
        [LabelText("位置")]public Vector3 Position;
        [LabelText("大小")]public float Scale;

        public override float length
        {
            get { return _length; }
            set { _length = value; }
        }

        public override bool isValid => true;

        public void SetLine(int line)
        {
            _line = line;
        }
        
        protected override void OnRawUpdate()
        {
            if (cutscene.currentTime==0 || cutscene.currentTime < startTime || cutscene.currentTime > endTime)
            {
                return;
            }
            // Debug.Log($"Collide clip [{startTime}-{endTime}] draw at {cutscene.currentTime}");
            SkillDraw.SphereWithOutline(Position , Scale, Color.yellow.WithAlpha(0.5f));
        }
    }
}