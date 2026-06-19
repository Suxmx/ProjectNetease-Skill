using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    /// <summary>
    /// 移动位移 Clip（ClientPrediction 域）。
    /// 编辑器预览：在 active 区间内按 DisplacementPerSecond × 已过时间实际移动 Actor，
    /// 退出/回放时恢复原位。
    /// </summary>
    [SkillClipType(1002u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class MoveDisplacementClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 0.2f;
        [SkillCustomData, LabelText("Space")] public SkillSpace Space = SkillSpace.AimDirection;
        [SkillCustomData, LabelText("Displacement Per Second")] public Vector3 DisplacementPerSecond;

        public override float length
        {
            get { return _length; }
            set { _length = value; }
        }

        public override bool isValid => true;

        /// <summary>记录 Actor 进入 clip 时的初始位置，用于退出时恢复。</summary>
        private Vector3 _initialPosition;
        private bool _hasInitialPosition;

        protected override void OnEnter()
        {
            CacheInitialPosition();
            ApplyDisplacement(cutscene.currentTime);
        }

        protected override void OnReverse()
        {
            RestoreInitialPosition();
        }

        protected override void OnExit()
        {
            RestoreInitialPosition();
        }

        protected override void OnUpdate(float time, float previousTime)
        {
            ApplyDisplacement(time);
        }

        protected override void OnRawUpdate()
        {
            if (cutscene.currentTime == 0)
            {
                if (_hasInitialPosition)
                    RestoreInitialPosition();
                return;
            }

            if (cutscene.currentTime < startTime || cutscene.currentTime > endTime)
                return;

            if (!_hasInitialPosition)
                CacheInitialPosition();

            ApplyDisplacement(cutscene.currentTime);
        }

        protected override void OnFixedTick(int tick, int totalTicks)
        {
            if (actor == null)
                return;

            int startTick = SkillTickUtility.SecondsToTicks(startTime, 60);
            int elapsedTick = tick - startTick;
            if (elapsedTick < 0)
                return;

            if (!_hasInitialPosition)
                CacheInitialPosition();

            float elapsedSeconds = SkillTickUtility.TicksToSeconds(elapsedTick, 60);
            Vector3 displacement = ResolveDisplacement(elapsedSeconds);
            actor.transform.position = _initialPosition + displacement;
        }

        /// <summary>缓存 Actor 当前位置作为初始位置。</summary>
        private void CacheInitialPosition()
        {
            if (actor == null)
                return;
            _initialPosition = actor.transform.position;
            _hasInitialPosition = true;
        }

        /// <summary>恢复 Actor 到初始位置。</summary>
        private void RestoreInitialPosition()
        {
            if (actor != null && _hasInitialPosition)
                actor.transform.position = _initialPosition;
            _hasInitialPosition = false;
        }

        /// <summary>按当前时间应用位移到 Actor。</summary>
        private void ApplyDisplacement(float currentTime)
        {
            if (actor == null || !_hasInitialPosition)
                return;

            float elapsed = Mathf.Max(0f, currentTime - startTime);
            Vector3 displacement = ResolveDisplacement(elapsed);
            actor.transform.position = _initialPosition + displacement;
        }

        /// <summary>根据 Space 将 DisplacementPerSecond × elapsed 转换为世界位移。</summary>
        private Vector3 ResolveDisplacement(float elapsedSeconds)
        {
            Transform actorTransform = actor != null ? actor.transform : null;
            if (actorTransform == null)
                return DisplacementPerSecond * elapsedSeconds;

            return SkillPreviewUtility.ResolveVector(Space, DisplacementPerSecond, actorTransform, actorTransform.forward) * elapsedSeconds;
        }
    }
}
