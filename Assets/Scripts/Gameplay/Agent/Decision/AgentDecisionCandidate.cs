using Gameplay.Agent.Data;
using UnityEngine;

namespace Gameplay.Agent.Decision
{
    /// <summary>
    /// Agent 目标决策候选
    /// 由目标发现层收集，决策服务只读取这些快照，不反查场景对象
    /// </summary>
    public readonly struct AgentDecisionCandidate
    {
        public AgentDecisionCandidate(
            AgentDecisionTargetKind decisionTargetKind,
            AgentDirectiveType directiveType,
            AgentTargetKind targetKind,
            string targetId,
            GameObject targetObject,
            Vector3 targetPosition,
            float distanceSqr,
            global::EnemyHealthController riskEnemy,
            bool isCurrentTarget)
        {
            DecisionTargetKind = decisionTargetKind;
            DirectiveType = directiveType;
            TargetKind = targetKind;
            TargetId = targetId ?? string.Empty;
            TargetObject = targetObject;
            TargetPosition = targetPosition;
            DistanceSqr = Mathf.Max(0f, distanceSqr);
            RiskEnemy = riskEnemy;
            IsCurrentTarget = isCurrentTarget;
        }

        public AgentDecisionTargetKind DecisionTargetKind { get; }
        public AgentDirectiveType DirectiveType { get; }
        public AgentTargetKind TargetKind { get; }
        public string TargetId { get; }
        public GameObject TargetObject { get; }
        public Vector3 TargetPosition { get; }
        public float DistanceSqr { get; }
        public global::EnemyHealthController RiskEnemy { get; }
        public bool IsCurrentTarget { get; }
        public bool IsValid => DecisionTargetKind != AgentDecisionTargetKind.None && TargetObject != null;
    }
}
