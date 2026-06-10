using UnityEngine;

namespace Gameplay.Agent.Combat
{
    public readonly struct AgentCombatSkillContext
    {
        public AgentCombatSkillContext(
            Transform casterTransform,
            AgentCombatStyleConfig styleConfig,
            AgentCombatRuntimeStats stats,
            LayerMask enemyLayerMask,
            float actionLockSeconds)
        {
            CasterTransform = casterTransform;
            StyleConfig = styleConfig;
            Stats = stats;
            EnemyLayerMask = enemyLayerMask;
            ActionLockSeconds = Mathf.Max(0.05f, actionLockSeconds);
        }

        public Transform CasterTransform { get; }
        public GameObject SourceObject => CasterTransform != null ? CasterTransform.gameObject : null;
        public AgentCombatStyleConfig StyleConfig { get; }
        public AgentCombatRuntimeStats Stats { get; }
        public LayerMask EnemyLayerMask { get; }
        public float ActionLockSeconds { get; }

        public Vector3 Position => CasterTransform != null ? CasterTransform.position : Vector3.zero;

        public Vector3 Forward
        {
            get
            {
                if (CasterTransform == null)
                    return Vector3.forward;

                Vector3 forward = CasterTransform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            }
        }
    }
}
