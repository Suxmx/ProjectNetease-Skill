using Slate;
using UnityEngine;

namespace Hoshino
{
    [Attachable(typeof(CollisionTrack))]
    public class TestSimpleClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 3f;

        public override float length
        {
            get { return _length; }
            set { _length = value; }
        }

        public override bool isValid => true;
        [SerializeField]
        [HideInInspector]
        private float _blendIn = 0;
        [SerializeField]
        [HideInInspector]
        private float _blendOut = 0;
        public override float blendIn {
            get { return _blendIn; }
            set { _blendIn = value; }
        }

        public override float blendOut {
            get { return _blendOut; }
            set { _blendOut = value; }
        }

        public override bool canCrossBlend => true;
    }
}