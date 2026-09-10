using System.Collections.Generic;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Targeting;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Decision
{
    /// <summary>
    /// Agent 目标决策组件
    /// 按 Agent 自身发现范围收集候选目标，使用决策配置评分后向 Brain 投递当前目标指令
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AgentPawnRoot))]
    public sealed class AgentTargetDecisionController : MonoBehaviour
    {
        [Header("决策配置")]
        [SerializeField] private bool _enableDecisionModule = true;
        [SerializeField] private AgentDecisionConfig _decisionConfig;

        [Header("组件引用")]
        [SerializeField] private AgentPawnRoot _pawnRoot;

        private readonly List<GameplayTargetClusterAuthoringBase> _clusterBuffer =
            new List<GameplayTargetClusterAuthoringBase>();

        private readonly List<AgentDecisionCandidate> _decisionCandidateBuffer =
            new List<AgentDecisionCandidate>();

        private readonly List<global::EnemyHealthController> _riskEnemyBuffer =
            new List<global::EnemyHealthController>();

        private AgentDecisionConfig _cachedDecisionConfig;
        private AgentTargetDecisionService _decisionService;
        private double _nextScanTime;

        public bool IsDecisionModuleActive => isActiveAndEnabled && _enableDecisionModule && _decisionConfig != null;

        private void Reset()
        {
            CacheComponents();
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            _collector.StartObservingFailures();
            _nextScanTime = 0d;
        }

        private void OnDisable()
        {
            _collector.StopObservingFailures();
            CacheComponents();
            if (_pawnRoot == null || _pawnRoot.Blackboard == null)
                return;

            WriteDecisionDisabled(_pawnRoot, "决策组件已停用");
            ClearTargetFacts(_pawnRoot);
        }

        private void Update()
        {
            CacheComponents();
            if (_pawnRoot == null || !_pawnRoot.EnableTargetDiscovery || _pawnRoot.IsDead)
                return;

            double timeSeconds = Time.timeAsDouble;
            if (timeSeconds < _nextScanTime)
                return;

            ScheduleNextScan(timeSeconds);
            RefreshTarget();
        }

        /// <summary>
        /// 手动立即刷新一次目标决策
        /// </summary>
        [ContextMenu("刷新目标决策")]
        public void RefreshTarget()
        {
            CacheComponents();
            if (_pawnRoot == null || _pawnRoot.Blackboard == null)
                return;

            IAgentReadOnly agent = _pawnRoot;
            IAgentCommandReceiver commandReceiver = _pawnRoot;

            if (AgentManualDirectiveLock.ShouldHoldManualDirective(agent))
            {
                WriteDecisionDisabled(agent, "手动目标指令锁定中");
                return;
            }

            if (AgentManualDirectiveLock.ShouldHoldCombatDamageDirective(agent))
            {
                WriteDecisionDisabled(agent, "受击反击指令锁定中");
                return;
            }

            if (!_enableDecisionModule || _decisionConfig == null)
            {
                WriteDecisionDisabled(agent, "决策组件未启用或缺少 AgentDecisionConfig");
                return;
            }

            float range = Mathf.Max(0f, _pawnRoot.TargetDiscoveryRange);
            float rangeSqr = range * range;
            AgentDecisionContext decisionContext = BuildDecisionContext(agent);

            if (range <= 0f)
            {
                WriteDecisionFailure(agent, decisionContext, 0, 0, "发现范围为 0，清空目标");
                ClearTargetFacts(commandReceiver);
                return;
            }

            GameplayTargetRegistry targetRegistry = GameplayTargetRegistry.GetOrCreate();
            BuildDecisionCandidates(targetRegistry, agent, rangeSqr);
            decisionContext = BuildDecisionContext(agent);

            AgentTargetDecisionService decisionService = GetDecisionService();
            int candidateCount = _decisionCandidateBuffer.Count + _farExitCandidates.Count;
            int riskEnemyCount = _riskEnemyBuffer.Count;
            if (!decisionService.TryChooseTarget(
                    decisionContext,
                    _decisionCandidateBuffer,
                    out AgentDecisionResult result) &&
                !decisionService.TryChooseTarget(decisionContext,_farExitCandidates,out result))
            {
                WriteDecisionFailure(agent, decisionContext, candidateCount, riskEnemyCount, result.Reason);
                ClearTargetFacts(commandReceiver);
                return;
            }

            ApplyDecisionTarget(commandReceiver, result);
            WriteDecisionResult(agent, decisionContext, candidateCount, riskEnemyCount, result);
        }

        private void CacheComponents()
        {
            if (_pawnRoot == null)
                _pawnRoot = GetComponent<AgentPawnRoot>();
        }

        private void ScheduleNextScan(double timeSeconds)
        {
            float interval = Mathf.Max(0.05f, _pawnRoot.TargetDiscoveryInterval);
            _nextScanTime = timeSeconds + interval;
        }

        // 收集所有范围内候选目标，让决策服务统一评分
        private readonly AgentTargetCandidateCollector _collector=new AgentTargetCandidateCollector();
        private readonly List<AgentTargetCandidate> _enemyCandidates=new List<AgentTargetCandidate>();
        private readonly List<AgentTargetCandidate> _worldCandidates=new List<AgentTargetCandidate>();
        private readonly List<AgentDecisionCandidate> _farExitCandidates=new List<AgentDecisionCandidate>();

        private void BuildDecisionCandidates(GameplayTargetRegistry targetRegistry, IAgentReadOnly agent, float rangeSqr)
        {
            _decisionCandidateBuffer.Clear(); _riskEnemyBuffer.Clear(); _farExitCandidates.Clear();
            targetRegistry.CopyClustersTo(_clusterBuffer);
            _collector.CollectVisibleEnemies(agent,_clusterBuffer,Mathf.Sqrt(rangeSqr),_enemyCandidates);
            _collector.CollectWorldTargets(agent,_clusterBuffer,Mathf.Sqrt(rangeSqr),_worldCandidates);
            string currentTargetId=ResolveCurrentTargetId(agent);
            foreach (var candidate in _enemyCandidates)
            {
                _riskEnemyBuffer.Add(candidate.Enemy);
                if (!candidate.CanExecute) continue;
                string id=ResolveActiveEnemyDecisionTargetId(targetRegistry,(ActiveEnemyClusterAuthoring)candidate.Cluster,candidate.Enemy);
                _decisionCandidateBuffer.Add(new AgentDecisionCandidate(AgentDecisionTargetKind.ActiveEnemy,AgentDirectiveType.Engage,
                    AgentTargetKind.Enemy,id,candidate.Member,candidate.Position,candidate.DistanceSqr,candidate.Enemy,IsCurrentTarget(id,currentTargetId)));
            }
            foreach (var candidate in _worldCandidates)
            {
                string id=candidate.Cluster.TargetId;
                bool resource=candidate.Kind==AgentTargetKind.Resource, exit=candidate.Kind==AgentTargetKind.Extraction;
                var snapshot=new AgentDecisionCandidate(resource?AgentDecisionTargetKind.Resource:exit?AgentDecisionTargetKind.Extraction:AgentDecisionTargetKind.EnemySource,
                    resource?AgentDirectiveType.Search:exit?AgentDirectiveType.Extract:AgentDirectiveType.MoveTo,candidate.Kind,id,
                    resource?candidate.Cluster.gameObject:candidate.Member,candidate.Position,candidate.DistanceSqr,null,IsCurrentTarget(id,currentTargetId));
                if (exit && candidate.DistanceSqr>rangeSqr) _farExitCandidates.Add(snapshot);
                else _decisionCandidateBuffer.Add(snapshot);
            }
        }

        private AgentDecisionContext BuildDecisionContext(IAgentReadOnly agent)
        {
            float attack = 1f;
            if (agent.Blackboard != null)
                agent.Blackboard.TryGetValue(AgentBlackboardKeys.AttackDamage, out attack);

            return new AgentDecisionContext(
                agent.Position,
                attack,
                agent.CurrentHealth,
                agent.MaxHealth,
                _pawnRoot.Defense,
                ResolveCurrentTargetId(agent),
                _riskEnemyBuffer);
        }

        private void ApplyDecisionTarget(
            IAgentCommandReceiver commandReceiver,
            AgentDecisionResult result)
        {
            AgentDecisionCandidate candidate = result.Candidate;

            commandReceiver.SubmitDirective(new AgentDirectiveRequest(
                candidate.DirectiveType,
                AgentTargetRef.FromConcreteObject(
                    candidate.TargetKind,
                    candidate.TargetObject,
                    candidate.TargetId),
                candidate.TargetId,
                _pawnRoot.AgentId));
        }

        private AgentTargetDecisionService GetDecisionService()
        {
            if (_decisionService != null && _cachedDecisionConfig == _decisionConfig)
                return _decisionService;

            _cachedDecisionConfig = _decisionConfig;
            _decisionService = new AgentTargetDecisionService(_decisionConfig);
            return _decisionService;
        }

        private static string ResolveCurrentTargetId(IAgentReadOnly agent)
        {
            if (agent?.Blackboard == null ||
                !agent.Blackboard.TryGetValue(
                    AgentBlackboardKeys.PendingDirectiveRequest,
                    out AgentDirectiveRequest directiveRequest))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(directiveRequest.TargetId))
                return directiveRequest.TargetId;

            return directiveRequest.TargetRef.TargetId;
        }

        private static bool IsCurrentTarget(string candidateTargetId, string currentTargetId)
        {
            return !string.IsNullOrWhiteSpace(candidateTargetId) &&
                   string.Equals(candidateTargetId, currentTargetId, System.StringComparison.Ordinal);
        }

        private static string ResolveActiveEnemyDecisionTargetId(
            GameplayTargetRegistry targetRegistry,
            ActiveEnemyClusterAuthoring enemyCluster,
            global::EnemyHealthController enemy)
        {
            if (targetRegistry != null &&
                targetRegistry.TryFindEnemySourceTargetIdByEnemy(enemy, out string sourceTargetId))
            {
                return sourceTargetId;
            }

            return enemyCluster != null ? enemyCluster.TargetId : string.Empty;
        }

        private static void ClearTargetFacts(IAgentCommandReceiver commandReceiver)
        {
            commandReceiver.SetVisibleEnemy(false);
            commandReceiver.SetHasEnemySourceTarget(false);
            commandReceiver.SetHasResourceTarget(false);
            commandReceiver.SetHasInteractableTarget(false);
            commandReceiver.SetShouldExtract(false);
            commandReceiver.ClearDirective();
        }

        private static void WriteDecisionDisabled(IAgentReadOnly agent, string reason)
        {
            if (agent?.Blackboard == null)
                return;

            double timeSeconds = Time.timeAsDouble;
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionModuleEnabled, false, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionTargetId, string.Empty, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionTargetKind, AgentDecisionTargetKind.None, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionScore, 0f, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionRisk, 0f, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionCandidateCount, 0, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionRiskEnemyCount, 0, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionAttack, 0f, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionDefense, 0f, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionReason, reason ?? string.Empty, timeSeconds);
        }

        private static void WriteDecisionFailure(
            IAgentReadOnly agent,
            AgentDecisionContext context,
            int candidateCount,
            int riskEnemyCount,
            string reason)
        {
            if (agent?.Blackboard == null)
                return;

            double timeSeconds = Time.timeAsDouble;
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionModuleEnabled, true, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionTargetId, string.Empty, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionTargetKind, AgentDecisionTargetKind.None, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionScore, 0f, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionRisk, 0f, timeSeconds);
            WriteDecisionContextFacts(agent, context, candidateCount, riskEnemyCount, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionReason, reason ?? string.Empty, timeSeconds);
        }

        private static void WriteDecisionResult(
            IAgentReadOnly agent,
            AgentDecisionContext context,
            int candidateCount,
            int riskEnemyCount,
            AgentDecisionResult result)
        {
            if (agent?.Blackboard == null)
                return;

            double timeSeconds = Time.timeAsDouble;
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionModuleEnabled, true, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionTargetId, result.Candidate.TargetId, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionTargetKind, result.Candidate.DecisionTargetKind, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionScore, result.Score, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionRisk, result.Risk, timeSeconds);
            WriteDecisionContextFacts(agent, context, candidateCount, riskEnemyCount, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionReason, result.Reason, timeSeconds);
        }

        // 将本轮决策输入写回黑板，便于 Inspector 判断评分结果是否来自候选收集或风险估算
        private static void WriteDecisionContextFacts(
            IAgentReadOnly agent,
            AgentDecisionContext context,
            int candidateCount,
            int riskEnemyCount,
            double timeSeconds)
        {
            agent.Blackboard.SetValue(
                AgentBlackboardKeys.DecisionCandidateCount,
                Mathf.Max(0, candidateCount),
                timeSeconds);
            agent.Blackboard.SetValue(
                AgentBlackboardKeys.DecisionRiskEnemyCount,
                Mathf.Max(0, riskEnemyCount),
                timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionAttack, context.Attack, timeSeconds);
            agent.Blackboard.SetValue(AgentBlackboardKeys.DecisionDefense, context.Defense, timeSeconds);
        }

    }
}
