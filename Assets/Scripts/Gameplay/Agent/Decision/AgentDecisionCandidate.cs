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
            bool isCurrentTarget,
            bool hasRouteEstimate = false,
            float routeRisk = 0f)
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
            HasRouteEstimate = hasRouteEstimate;
            RouteRisk = Mathf.Max(0f, routeRisk);
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

        /// <summary>
        /// 距离来自作者图路程估算，风险已包含途经敌群
        /// </summary>
        public bool HasRouteEstimate { get; }

        /// <summary>
        /// 路线途经节点上存活敌人的风险合计，不含目标敌人本身
        /// </summary>
        public float RouteRisk { get; }
        public bool IsValid => DecisionTargetKind != AgentDecisionTargetKind.None && TargetObject != null;
    }
}
