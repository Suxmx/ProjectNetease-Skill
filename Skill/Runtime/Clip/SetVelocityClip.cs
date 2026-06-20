using Sirenix.OdinInspector;
using Slate;
using UnityEngine;

namespace Hoshino
{
    /// <summary>
    /// 移动速度 Clip（ClientPrediction 域，id=1002）。
    /// 按 Velocity（每秒速度）施加预测速度到 Motor，由 PredictionRigidbody.Simulate 连续推进物理，
    /// 相比瞬移式位移更符合物理、reconcile 更干净，突进结束速度归零即硬停。
    /// 编辑器预览：在 active 区间内按 Velocity × 已过时间实际移动 Actor，退出/回放时恢复原位。
    /// </summary>
    [SkillClipType(1002u)]
    [Attachable(typeof(SkillActionTrack))]
    public sealed class SetVelocityClip : ActionClip
    {
        [SerializeField, HideInInspector] private float _length = 0.2f;
        [SkillCustomData, LabelText("Space")] public SkillSpace Space = SkillSpace.AimDirection;
        [SkillCustomData, LabelText("Velocity")] public Vector3 Velocity;
        /// <summary>速度系数曲线（时间归一化 0-1，值 0-1）。null/空按 1 处理（恒速）。结束段衰减可消除 TickSmoother 追位拖拽。</summary>
        [SkillCustomData, LabelText("Velocity Curve")] public AnimationCurve VelocityCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

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
            ApplyOffset(cutscene.currentTime);
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
            ApplyOffset(time);
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

            ApplyOffset(cutscene.currentTime);
        }

        protected override void OnFixedTick(int tick, int totalTicks)
        {
            if (actor == null)
                return;
            if (cutscene.currentTime < startTime || cutscene.currentTime > endTime)
                return;

            int startTick = SkillTickUtility.SecondsToTicks(startTime, 60);
            int elapsedTick = tick - startTick;
            if (elapsedTick < 0)
                return;

            if (!_hasInitialPosition)
                CacheInitialPosition();

            float elapsedSeconds = SkillTickUtility.TicksToSeconds(elapsedTick, 60);
            Vector3 offset = ResolveOffset(elapsedSeconds);
            actor.transform.position = _initialPosition + offset;
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

        /// <summary>按当前时间应用位移到 Actor（预览：Velocity × elapsed）。</summary>
        private void ApplyOffset(float currentTime)
        {
            if (actor == null || !_hasInitialPosition)
                return;

            float elapsed = Mathf.Max(0f, currentTime - startTime);
            Vector3 offset = ResolveOffset(elapsed);
            actor.transform.position = _initialPosition + offset;
        }

        /// <summary>根据 Space 将 Velocity × elapsedSeconds 转换为世界位移，并乘以曲线系数。</summary>
        private Vector3 ResolveOffset(float elapsedSeconds)
        {
            Transform actorTransform = actor != null ? actor.transform : null;
            if (actorTransform == null)
                return Velocity * EvaluateCurve(elapsedSeconds) * elapsedSeconds;

            return SkillPreviewUtility.ResolveVector(Space, Velocity, actorTransform, actorTransform.forward) * EvaluateCurve(elapsedSeconds) * elapsedSeconds;
        }

        /// <summary>求曲线系数：按归一化时间 t∈[0,1] 求值，null/空曲线返回 1（恒速兼容）。</summary>
        private float EvaluateCurve(float elapsedSeconds)
        {
            if (VelocityCurve == null || VelocityCurve.length == 0)
                return 1f;
            float t = length > 0f ? Mathf.Clamp01(elapsedSeconds / length) : 1f;
            return VelocityCurve.Evaluate(t);
        }
    }
}
