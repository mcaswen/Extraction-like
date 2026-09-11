using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Navigation;
using Gameplay.Perception;
using Gameplay.Targets.Authoring;
using UnityEngine;

namespace Gameplay.Agent.Commands
{
    public static class AgentDirectiveValidationService
    {
        public static AgentDirectiveFailure Validate(IAgentReadOnly agent, AgentDirectiveRequest request)
        {
            if (agent == null || agent.CachedTransform == null || agent.IsDead || !agent.CachedTransform.gameObject.activeInHierarchy)
                return AgentDirectiveFailure.AgentUnavailable;
            if (agent is Behaviour behaviour && !behaviour.isActiveAndEnabled) return AgentDirectiveFailure.AgentUnavailable;
            AgentDirectiveFailure targetFailure = ValidateTarget(request);
            if (targetFailure != AgentDirectiveFailure.None) return targetFailure;
            if (request.DirectiveType == AgentDirectiveType.RequestBuff) return AgentDirectiveFailure.None;
            if (request.DirectiveType == AgentDirectiveType.Engage && request.TargetObject != null)
            {
                float range = agent.Blackboard.TryGetValue(AgentBlackboardKeys.AttackRange, out float value) ? value : 0f;
                if (TargetVisibilityQuery.Check(agent.CachedTransform, CombatAimPointResolver.Resolve(agent.CachedTransform),
                    request.TargetObject.transform, range) == TargetVisibilityResult.Visible) return AgentDirectiveFailure.None;
            }
            if (!AgentNavigationQuery.IsReady(agent.NavMeshAgent)) return AgentDirectiveFailure.NavigationNotReady;
            if (!TryResolveDestination(agent, request, out Vector3 destination)) return AgentDirectiveFailure.Unreachable;
            var path = AgentNavigationQuery.Check(agent.NavMeshAgent, destination, 0f);
            return path.Failed ? AgentDirectiveFailure.Unreachable : AgentDirectiveFailure.None;
        }

        public static AgentDirectiveFailure ValidateTarget(AgentDirectiveRequest request)
        {
            if (request.DirectiveType == AgentDirectiveType.None || !request.TargetRef.IsValid) return AgentDirectiveFailure.InvalidTarget;
            GameObject obj = request.TargetObject;
            if (request.TargetRef.IsConcreteObject && (obj == null || !obj.activeInHierarchy)) return AgentDirectiveFailure.InvalidTarget;
            if (obj == null) return AgentDirectiveFailure.None;
            if (obj.TryGetComponent(out global::ExtractionPointController point) && !point.isActiveAndEnabled) return AgentDirectiveFailure.InvalidTarget;
            if (obj.TryGetComponent(out GameplayTargetAuthoringBase authoring) && !authoring.isActiveAndEnabled) return AgentDirectiveFailure.InvalidTarget;
            if (obj.TryGetComponent(out global::EnemyHealthController enemy) && !enemy.IsAlive) return AgentDirectiveFailure.TargetCompleted;
            if (obj.TryGetComponent(out GameplayTargetAuthoringBase target) && target.HasBeenCompleted) return AgentDirectiveFailure.TargetCompleted;
            return AgentDirectiveFailure.None;
        }

        public static bool TryResolveDestination(IAgentReadOnly agent, AgentDirectiveRequest request, out Vector3 position)
        {
            GameObject obj = request.TargetObject;
            position = obj != null ? obj.transform.position : request.TargetPosition;
            if (obj == null) return request.HasTargetPosition;
            if (request.DirectiveType == AgentDirectiveType.Engage && obj.TryGetComponent(out global::EnemyHealthController enemy))
                position = AgentCombatNavigationTarget.Resolve(enemy);
            if (obj.TryGetComponent(out ResourceClusterAuthoring resource))
                return resource.TryGetNearestReachableIncompleteResource(agent.Position, agent.NavMeshAgent, out _, out position);
            if (obj.TryGetComponent(out ExtractionClusterAuthoring exit))
            {
                if (!exit.TryGetNearestExtractionPoint(agent.Position, out global::ExtractionPointController point)) return false;
                position = point.transform.position;
            }
            else if (request.TargetRef.Kind == AgentTargetKind.Resource)
            {
                float best = float.PositiveInfinity;
                foreach (Collider collider in obj.GetComponentsInChildren<Collider>())
                {
                    if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    Vector3 candidate = collider.ClosestPoint(agent.Position);
                    float distance = (candidate - agent.Position).sqrMagnitude;
                    if (distance < best) { best = distance; position = candidate; }
                }
            }
            return true;
        }
    }
}
