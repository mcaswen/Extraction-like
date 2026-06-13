using System.Collections.Generic;
using System.Text;
using Core.BehaviorTree.Blackboard;
using Gameplay.Agent.AI.Actions;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
using Gameplay.Targets.Authoring;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Runtime
{
    /// <summary>
    /// 运行时 Agent 控制台快照。
    /// 按 I 一次性打印目标、状态、NavMesh 和移动控制信息，便于排查卡点。
    /// </summary>
    public sealed class AgentRuntimeConsoleDebugDumper : MonoBehaviour
    {
        private static AgentRuntimeConsoleDebugDumper _activeInstance;

        [SerializeField] private KeyCode _dumpKey = KeyCode.I;
        [SerializeField, Min(0.1f)] private float _navMeshSampleRadius =
            AgentActionNodeBase.NavMeshTargetSampleRadius;

        private readonly List<AgentRuntimeHandle> _agentBuffer = new List<AgentRuntimeHandle>();
        private readonly StringBuilder _builder = new StringBuilder(8192);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            _activeInstance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeDumper()
        {
            if (_activeInstance != null)
                return;

            _activeInstance = Object.FindObjectOfType<AgentRuntimeConsoleDebugDumper>();
            if (_activeInstance != null)
                return;

            GameObject dumperObject = new GameObject("[AgentRuntimeConsoleDebugDumper]");
            _activeInstance = dumperObject.AddComponent<AgentRuntimeConsoleDebugDumper>();
        }

        private void Awake()
        {
            if (_activeInstance != null && _activeInstance != this)
            {
                enabled = false;
                return;
            }

            _activeInstance = this;
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
                _activeInstance = null;
        }

        private void Update()
        {
            if (_dumpKey == KeyCode.None || !Input.GetKeyDown(_dumpKey))
                return;

            DumpAllAgents();
        }

        /// <summary>
        /// 打印当前所有已注册 Agent 的运行时目标、黑板、NavMesh 和移动控制快照
        /// </summary>
        [ContextMenu("Dump All Agents")]
        public void DumpAllAgents()
        {
            AgentRuntimeRegistry registry = AgentRuntimeRegistry.GetOrCreate();
            registry.CopyHandlesTo(_agentBuffer);

            _builder.Clear();
            _builder.AppendLine($"[AgentDebugDump] time={Time.time:0.000} agents={_agentBuffer.Count}");

            if (_agentBuffer.Count <= 0)
            {
                _builder.AppendLine("没有注册中的 Agent。");
                Debug.Log(_builder.ToString(), this);
                return;
            }

            for (int i = 0; i < _agentBuffer.Count; i++)
            {
                AppendAgentSnapshot(_agentBuffer[i], i + 1, _agentBuffer.Count);
            }

            Debug.Log(_builder.ToString(), this);
        }

        private void AppendAgentSnapshot(AgentRuntimeHandle handle, int index, int total)
        {
            if (!handle.IsValid || handle.PawnRoot == null)
            {
                _builder.AppendLine($"--- Agent {index}/{total}: invalid handle");
                return;
            }

            AgentPawnRoot pawn = handle.PawnRoot;
            BehaviorBlackboard blackboard = pawn.Blackboard;
            Vector3 position = pawn.Position;

            _builder.AppendLine($"--- Agent {index}/{total}: {pawn.name}");
            _builder.AppendLine(
                $"id={pawn.AgentIdValue} state={pawn.CurrentMacroStateId}/{ResolveStateName(pawn, blackboard)} " +
                $"health={pawn.CurrentHealth}/{pawn.MaxHealth} dead={pawn.IsDead}");
            _builder.AppendLine(
                $"position={FormatVector(position)} forward={FormatVector(pawn.Forward)} " +
                $"targetDiscovery(enabled={pawn.EnableTargetDiscovery}, range={pawn.TargetDiscoveryRange:0.###}, interval={pawn.TargetDiscoveryInterval:0.###})");

            AppendBlackboardFacts(blackboard);
            AppendDecisionSnapshot(blackboard);
            AppendDirectiveSnapshot(blackboard, pawn);
            AppendNavMeshSnapshot(pawn);
            AppendMovementSnapshot(pawn);
        }

        private void AppendBlackboardFacts(BehaviorBlackboard blackboard)
        {
            if (blackboard == null)
            {
                _builder.AppendLine("facts: blackboard=null");
                return;
            }

            _builder.AppendLine(
                "facts: " +
                $"visibleEnemy={GetBlackboardValue(blackboard, AgentBlackboardKeys.HasVisibleEnemy, false)} " +
                $"enemySource={GetBlackboardValue(blackboard, AgentBlackboardKeys.HasEnemySourceTarget, false)} " +
                $"resource={GetBlackboardValue(blackboard, AgentBlackboardKeys.HasResourceTarget, false)} " +
                $"interactable={GetBlackboardValue(blackboard, AgentBlackboardKeys.HasInteractableTarget, false)} " +
                $"extract={GetBlackboardValue(blackboard, AgentBlackboardKeys.ShouldExtract, false)} " +
                $"pendingDirective={GetBlackboardValue(blackboard, AgentBlackboardKeys.HasPendingDirective, false)} " +
                $"needRecovery={GetBlackboardValue(blackboard, AgentBlackboardKeys.NeedRecovery, false)}");
        }

        private void AppendDecisionSnapshot(BehaviorBlackboard blackboard)
        {
            if (blackboard == null)
                return;

            bool enabledDecision = GetBlackboardValue(
                blackboard,
                AgentBlackboardKeys.DecisionModuleEnabled,
                false);
            AgentDecisionTargetKind targetKind = GetBlackboardValue(
                blackboard,
                AgentBlackboardKeys.DecisionTargetKind,
                AgentDecisionTargetKind.None);

            _builder.AppendLine(
                "decision: " +
                $"enabled={enabledDecision} kind={targetKind} " +
                $"targetId={GetBlackboardValue(blackboard, AgentBlackboardKeys.DecisionTargetId, string.Empty)} " +
                $"score={GetBlackboardValue(blackboard, AgentBlackboardKeys.DecisionScore, 0f):0.###} " +
                $"risk={GetBlackboardValue(blackboard, AgentBlackboardKeys.DecisionRisk, 0f):0.###} " +
                $"candidates={GetBlackboardValue(blackboard, AgentBlackboardKeys.DecisionCandidateCount, 0)} " +
                $"riskEnemies={GetBlackboardValue(blackboard, AgentBlackboardKeys.DecisionRiskEnemyCount, 0)} " +
                $"reason={GetBlackboardValue(blackboard, AgentBlackboardKeys.DecisionReason, string.Empty)}");
        }

        private void AppendDirectiveSnapshot(BehaviorBlackboard blackboard, AgentPawnRoot pawn)
        {
            if (blackboard == null ||
                !blackboard.TryGetValue(
                    AgentBlackboardKeys.PendingDirectiveRequest,
                    out AgentDirectiveRequest directive))
            {
                _builder.AppendLine("directive: none");
                return;
            }

            AgentTargetRef targetRef = directive.TargetRef;
            GameObject targetObject = targetRef.TargetObject;
            Vector3 targetPosition = targetRef.HasTargetPosition ? targetRef.TargetPosition : default;

            // 先打印黑板中保存的原始指令目标，保留手动点击或决策模块的输入信息
            _builder.AppendLine(
                "directive: " +
                $"type={directive.DirectiveType} target={targetRef.Kind}/{targetRef.BindingType} " +
                $"id={targetRef.TargetId} object={FormatObject(targetObject)} " +
                $"refPos={FormatVector(targetPosition)} hasRefPos={targetRef.HasTargetPosition} " +
                $"payload={directive.PayloadId} command={directive.CommandId} priority={directive.Priority}");

            if (targetRef.HasTargetPosition)
            {
                _builder.AppendLine(
                    "directiveDistance: " +
                    $"world={Vector3.Distance(pawn.Position, targetPosition):0.###} " +
                    $"planar={GetPlanarDistance(pawn.Position, targetPosition):0.###}");
            }

            // 再解析行为节点实际会追踪的实例目标，例如资源群内的具体箱子
            if (TryResolveNavigationTarget(
                    targetRef,
                    pawn.Position,
                    pawn.NavMeshAgent,
                    out ResourceClusterAuthoring debugResourceCluster,
                    out GameObject resolvedObject,
                    out Vector3 resolvedPosition,
                    out string resolvedReason))
            {
                _builder.AppendLine(
                    "resolvedMoveTarget: " +
                    $"reason={resolvedReason} object={FormatObject(resolvedObject)} " +
                    $"pos={FormatVector(resolvedPosition)} " +
                    $"world={Vector3.Distance(pawn.Position, resolvedPosition):0.###} " +
                    $"planar={GetPlanarDistance(pawn.Position, resolvedPosition):0.###}");

                AppendCalculatedPathSnapshot(pawn, resolvedPosition);
            }
            else
            {
                _builder.AppendLine("resolvedMoveTarget: unavailable");
            }

            // 资源群没有解析出具体资源时，追加成员级可达性明细
            if (debugResourceCluster != null && resolvedReason == "direct")
            {
                debugResourceCluster.AppendNavigationDebugSnapshot(
                    _builder,
                    pawn.Position,
                    pawn.NavMeshAgent);
            }
        }

        private void AppendNavMeshSnapshot(AgentPawnRoot pawn)
        {
            NavMeshAgent agent = pawn.NavMeshAgent;
            if (agent == null)
            {
                _builder.AppendLine("nav: agent=null");
                return;
            }

            bool enabledAgent = agent.enabled;
            bool isOnNavMesh = enabledAgent && agent.isOnNavMesh;
            _builder.Append(
                "nav: " +
                $"enabled={enabledAgent} onMesh={isOnNavMesh} stopped=");

            if (!isOnNavMesh)
            {
                _builder.AppendLine("n/a");
                return;
            }

            float remainingDistance = agent.remainingDistance;
            bool reliableRemaining =
                !agent.pathPending &&
                agent.hasPath &&
                agent.pathStatus == NavMeshPathStatus.PathComplete &&
                !float.IsNaN(remainingDistance) &&
                !float.IsInfinity(remainingDistance);

            _builder.AppendLine(
                $"{agent.isStopped} pending={agent.pathPending} hasPath={agent.hasPath} " +
                $"status={agent.pathStatus} dest={FormatVector(agent.destination)} " +
                $"remaining={FormatFloat(remainingDistance)} reliableRemaining={reliableRemaining} " +
                $"stopping={agent.stoppingDistance:0.###} speed={agent.speed:0.###} " +
                $"velocity={FormatVector(agent.velocity)} desired={FormatVector(agent.desiredVelocity)} " +
                $"steeringTarget={FormatVector(agent.steeringTarget)}");

            AppendPathCorners("navPath", agent.path);
        }

        private void AppendCalculatedPathSnapshot(AgentPawnRoot pawn, Vector3 targetPosition)
        {
            NavMeshAgent agent = pawn.NavMeshAgent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                return;

            // 目标位置先投到 NavMesh；起点交给 NavMeshAgent 自己处理，避免角色高度偏移影响调试结果
            bool sampledTarget = NavMesh.SamplePosition(
                targetPosition,
                out NavMeshHit targetHit,
                _navMeshSampleRadius,
                agent.areaMask);
            Vector3 startSourcePosition = agent.nextPosition;

            _builder.Append(
                "calcPathToResolved: " +
                $"sampleRadius={_navMeshSampleRadius:0.###} " +
                $"startSource=navMeshAgent.CalculatePath " +
                $"nextPosition={FormatVector(startSourcePosition)} " +
                $"transformToStart={Vector3.Distance(pawn.Position, startSourcePosition):0.###} " +
                $"targetSampled={sampledTarget}");
            if (sampledTarget)
                _builder.Append($" target={FormatVector(targetHit.position)} targetDelta={Vector3.Distance(targetPosition, targetHit.position):0.###}");

            if (!sampledTarget)
            {
                _builder.AppendLine();
                return;
            }

            NavMeshPath path = new NavMeshPath();
            // 这里故意使用实例 CalculatePath，与实际移动侧的起点语义保持一致
            bool calculated = agent.CalculatePath(targetHit.position, path);

            _builder.AppendLine(
                $" calculated={calculated} status={path.status} " +
                $"length={CalculatePathLength(path):0.###} corners={path.corners.Length}");
            AppendPathCorners("calcPathCorners", path);
        }

        private void AppendMovementSnapshot(AgentPawnRoot pawn)
        {
            Rigidbody body = pawn.GetComponent<Rigidbody>();
            global::PlayerMovementController movementController =
                pawn.GetComponent<global::PlayerMovementController>();

            _builder.Append(
                "movement: " +
                $"rigidbody={(body != null)}");

            if (body != null)
            {
                _builder.Append(
                    $" kinematic={body.isKinematic} gravity={body.useGravity} " +
                    $"rbVelocity={FormatVector(body.velocity)}");
            }

            _builder.AppendLine(
                $" playerController={(movementController != null)} " +
                $"navMeshDrivingRb={(movementController != null && movementController.IsNavMeshDrivingRigidbody)}");
        }

        private static bool TryResolveNavigationTarget(
            AgentTargetRef targetRef,
            Vector3 agentPosition,
            NavMeshAgent navMeshAgent,
            out ResourceClusterAuthoring debugResourceCluster,
            out GameObject resolvedObject,
            out Vector3 resolvedPosition,
            out string reason)
        {
            debugResourceCluster = null;
            resolvedObject = targetRef.TargetObject;
            reason = "direct";

            // 所有目标先解析为可展示的基础世界点；资源群稍后会尝试解析到具体成员
            if (!TryResolveTargetPosition(targetRef, out resolvedPosition))
                return false;

            // 资源群目标优先展开成当前具体可达资源成员
            if (targetRef.Kind == AgentTargetKind.Resource &&
                targetRef.TargetObject != null &&
                targetRef.TargetObject.TryGetComponent(out ResourceClusterAuthoring resourceCluster))
            {
                debugResourceCluster = resourceCluster;
                if (resourceCluster.TryGetNearestReachableIncompleteResource(
                        agentPosition,
                        navMeshAgent,
                        out GameObject resourceObject,
                        out Vector3 resourceNavigationPosition) &&
                    resourceObject != null)
                {
                    resolvedObject = resourceObject;
                    reason = $"nearestReachableResourceInCluster({resourceCluster.TargetId})";
                    resolvedPosition = resourceNavigationPosition;
                    return true;
                }
            }

            if (targetRef.Kind == AgentTargetKind.Extraction &&
                targetRef.TargetObject != null &&
                targetRef.TargetObject.TryGetComponent(out ExtractionClusterAuthoring extractionCluster))
            {
                if (extractionCluster.TryGetNearestExtractionPoint(
                        agentPosition,
                        out global::ExtractionPointController extractionPoint) &&
                    extractionPoint != null)
                {
                    resolvedObject = extractionPoint.gameObject;
                    reason = $"nearestExtractionPointInCluster({extractionCluster.TargetId})";
                    resolvedPosition = extractionPoint.transform.position;
                    return true;
                }

                reason = $"missingExtractionPointInCluster({extractionCluster.TargetId})";
                return false;
            }

            // 非群目标使用碰撞体最近点，方便观察真实交互停靠点而不是对象 pivot
            if (resolvedObject != null &&
                resolvedObject.GetComponent<GameplayTargetClusterAuthoringBase>() == null)
            {
                reason = "targetColliderClosestPoint";
                return TryResolveInteractionTargetPosition(
                    resolvedObject,
                    agentPosition,
                    out resolvedPosition);
            }

            return true;
        }

        private static bool TryResolveTargetPosition(AgentTargetRef targetRef, out Vector3 targetPosition)
        {
            if (!targetRef.IsValid)
            {
                targetPosition = default;
                return false;
            }

            GameObject targetObject = targetRef.TargetObject;
            if (targetObject != null)
            {
                if (targetObject.TryGetComponent(
                        out GameplayTargetClusterAuthoringBase clusterTarget))
                {
                    targetPosition = clusterTarget.CenterPosition;
                    return true;
                }

                targetPosition = targetObject.transform.position;
                return true;
            }

            if (targetRef.HasTargetPosition)
            {
                targetPosition = targetRef.TargetPosition;
                return true;
            }

            targetPosition = default;
            return false;
        }

        private static bool TryResolveInteractionTargetPosition(
            GameObject targetObject,
            Vector3 agentPosition,
            out Vector3 targetPosition)
        {
            targetPosition = targetObject != null ? targetObject.transform.position : default;
            if (targetObject == null)
                return false;

            Collider[] colliders = targetObject.GetComponentsInChildren<Collider>();
            if (colliders == null || colliders.Length <= 0)
                return true;

            bool hasClosestPoint = false;
            Vector3 closestPoint = targetPosition;
            float closestDistanceSqr = float.MaxValue;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider targetCollider = colliders[i];
                if (targetCollider == null ||
                    !targetCollider.enabled ||
                    !targetCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 candidatePoint = targetCollider.ClosestPoint(agentPosition);
                float distanceSqr = GetPlanarDistanceSqr(agentPosition, candidatePoint);
                if (distanceSqr >= closestDistanceSqr)
                    continue;

                hasClosestPoint = true;
                closestPoint = candidatePoint;
                closestDistanceSqr = distanceSqr;
            }

            if (hasClosestPoint)
                targetPosition = closestPoint;

            return true;
        }

        private void AppendPathCorners(string label, NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length <= 0)
            {
                _builder.AppendLine($"{label}: corners=0");
                return;
            }

            _builder.Append(
                $"{label}: length={CalculatePathLength(path):0.###} corners={path.corners.Length}");

            int cornerCount = Mathf.Min(path.corners.Length, 6);
            for (int i = 0; i < cornerCount; i++)
            {
                _builder.Append($" c{i}={FormatVector(path.corners[i])}");
            }

            if (path.corners.Length > cornerCount)
                _builder.Append(" ...");

            _builder.AppendLine();
        }

        private static T GetBlackboardValue<T>(
            BehaviorBlackboard blackboard,
            BlackboardKey key,
            T defaultValue)
        {
            return blackboard != null && blackboard.TryGetValue(key, out T value)
                ? value
                : defaultValue;
        }

        private static string ResolveStateName(
            AgentPawnRoot pawn,
            BehaviorBlackboard blackboard)
        {
            if (blackboard != null &&
                blackboard.TryGetValue(
                    AgentBlackboardKeys.CurrentMacroStateName,
                    out string stateName) &&
                !string.IsNullOrWhiteSpace(stateName))
            {
                return stateName;
            }

            return pawn.CurrentMacroStateName;
        }

        private static float CalculatePathLength(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
                return 0f;

            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            return length;
        }

        private static float GetPlanarDistance(Vector3 from, Vector3 to)
        {
            return Mathf.Sqrt(GetPlanarDistanceSqr(from, to));
        }

        private static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }

        private static string FormatObject(GameObject targetObject)
        {
            return targetObject != null ? targetObject.name : "null";
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private static string FormatFloat(float value)
        {
            return float.IsInfinity(value) || float.IsNaN(value)
                ? value.ToString()
                : value.ToString("0.###");
        }
    }
}
