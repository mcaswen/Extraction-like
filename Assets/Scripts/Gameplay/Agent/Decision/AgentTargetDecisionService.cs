using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Decision
{
    /// <summary>
    /// Agent 目标决策服务
    /// 只根据候选快照和配置评分，不直接修改黑板、目标注册表或场景对象
    /// </summary>
    public sealed class AgentTargetDecisionService
    {
        private readonly AgentDecisionConfig _config;

        public AgentTargetDecisionService(AgentDecisionConfig config)
        {
            _config = config;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>可选的遥测接收器；为空时评分不做任何额外工作</summary>
        public IAgentDecisionRoundSink Telemetry { get; set; }
#endif

        /// <summary>
        /// 从候选目标中选择最高评分目标
        /// </summary>
        /// <param name="context"></param>
        /// <param name="candidates"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public bool TryChooseTarget(
            AgentDecisionContext context,
            IReadOnlyList<AgentDecisionCandidate> candidates,
            out AgentDecisionResult result)
        {
            if (_config == null)
            {
                result = AgentDecisionResult.NoDecision("缺少 AgentDecisionConfig");
                return false;
            }

            if (candidates == null || candidates.Count <= 0)
            {
                result = AgentDecisionResult.NoDecision("没有可用候选目标");
                return false;
            }

            bool hasBest = false;
            AgentDecisionCandidate bestCandidate = default;
            float bestScore = float.NegativeInfinity;
            float bestRisk = 0f;
            string bestReason = string.Empty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            IAgentDecisionRoundSink telemetry = Telemetry;
            int bestIndex = -1;
#endif

            for (int index = 0; index < candidates.Count; index++)
            {
                AgentDecisionCandidate candidate = candidates[index];
                if (!candidate.IsValid)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    telemetry?.RecordCandidate(index, candidate, AgentCandidateVerdict.Invalid, default);
#endif
                    continue;
                }

                float risk = CalculateRisk(context, candidate);
                if (!CanAcceptCandidate(context, candidate, risk))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    telemetry?.RecordCandidate(index, candidate, RejectionVerdict(context, candidate),
                        new AgentScoreBreakdown(0f, risk, 0f, 0f, 0f, 0f, 0f, 0f, 0f, float.NaN));
#endif
                    continue;
                }

                float score = CalculateScore(context, candidate, risk);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (telemetry != null)
                    telemetry.RecordCandidate(index, candidate, AgentCandidateVerdict.NotBest, ExplainScore(context, candidate, risk));
#endif
                if (hasBest && score <= bestScore)
                    continue;

                hasBest = true;
                bestCandidate = candidate;
                bestScore = score;
                bestRisk = risk;
                bestReason = BuildReason(candidate, score, risk);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                bestIndex = index;
#endif
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            telemetry?.EndList(bestIndex);
#endif

            if (!hasBest)
            {
                result = AgentDecisionResult.NoDecision("所有候选目标都超过当前风险或生命约束");
                return false;
            }

            result = new AgentDecisionResult(
                true,
                bestCandidate,
                bestScore,
                bestRisk,
                bestReason);
            return true;
        }

        /// <summary>
        /// 按策划公式计算敌人节点风险值 RL
        /// </summary>
        /// <param name="context"></param>
        /// <param name="enemy"></param>
        /// <returns></returns>
        public float CalculateEnemyRisk(AgentDecisionContext context, global::EnemyHealthController enemy)
        {
            if (enemy == null || !enemy.IsAlive)
                return 0f;

            int enemyLevel = _config.EstimateEnemyLevel(enemy.MaxHealth);
            float denominator = Mathf.Max(1f, context.Attack + context.CurrentHealth + context.Defense);
            return _config.RiskScale * enemyLevel / denominator;
        }

        private float CalculateRisk(
            AgentDecisionContext context,
            AgentDecisionCandidate candidate)
        {
            float risk = 0f;

            if (candidate.DecisionTargetKind == AgentDecisionTargetKind.ActiveEnemy)
                risk += CalculateEnemyRisk(context, candidate.RiskEnemy);

            // 路线会处理途经的每个敌群，已有途经风险时不再用直线邻近估算
            if (candidate.HasRouteEstimate)
                return risk + candidate.RouteRisk;

            IReadOnlyList<global::EnemyHealthController> riskEnemies = context.RiskEnemies;
            if (riskEnemies == null || riskEnemies.Count <= 0)
                return risk;

            float influenceRadius = _config.EnemyPathInfluenceRadius;
            for (int index = 0; index < riskEnemies.Count; index++)
            {
                global::EnemyHealthController enemy = riskEnemies[index];
                if (enemy == null || enemy == candidate.RiskEnemy || !enemy.IsAlive)
                    continue;

                if (!IsEnemyNearPath(
                        context.AgentPosition,
                        candidate.TargetPosition,
                        enemy.transform.position,
                        influenceRadius))
                {
                    continue;
                }

                risk += CalculateEnemyRisk(context, enemy);
            }

            return risk;
        }

        private bool CanAcceptCandidate(
            AgentDecisionContext context,
            AgentDecisionCandidate candidate,
            float risk)
        {
            if (candidate.DecisionTargetKind == AgentDecisionTargetKind.ActiveEnemy &&
                context.HealthRatio < _config.MinCombatHealthRatio)
            {
                return false;
            }

            return risk <= _config.GetRiskThreshold(candidate.DecisionTargetKind);
        }

        private float CalculateScore(
            AgentDecisionContext context,
            AgentDecisionCandidate candidate,
            float risk)
        {
            float distance = Mathf.Sqrt(Mathf.Max(0f, candidate.DistanceSqr));
            float score = _config.GetBaseScore(candidate.DecisionTargetKind);
            // 回程路程和风险迟早要承担，撤离只按比例计入，避免远出口永远输给附近目标
            float penaltyScale = candidate.DecisionTargetKind == AgentDecisionTargetKind.Extraction
                ? _config.ExtractionPenaltyScale
                : 1f;
            score -= risk * _config.RiskPenaltyWeight * penaltyScale;
            score -= distance * _config.DistancePenaltyWeight * penaltyScale;

            if (candidate.IsCurrentTarget)
                score += _config.CurrentTargetBonus;

            if (candidate.DecisionTargetKind == AgentDecisionTargetKind.Extraction &&
                context.HealthRatio <= _config.LowHealthExtractionRatio)
            {
                score += _config.LowHealthExtractionBonus;
            }

            // 携带越多，损失风险越大，撤离收益随带出价值线性增加
            if (candidate.DecisionTargetKind == AgentDecisionTargetKind.Extraction)
            {
                float carriedRatio = Mathf.Clamp01(context.CarriedValue / _config.CarriedValueTarget);
                score += carriedRatio * _config.CarriedValueExtractionBonus;
            }

            return score;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 与 CalculateScore 相同的运算和顺序，只是保留中间值；遥测专用，不参与取舍
        public AgentScoreBreakdown ExplainScore(
            AgentDecisionContext context,
            AgentDecisionCandidate candidate,
            float risk)
        {
            float distance = Mathf.Sqrt(Mathf.Max(0f, candidate.DistanceSqr));
            float baseScore = _config.GetBaseScore(candidate.DecisionTargetKind);
            float penaltyScale = candidate.DecisionTargetKind == AgentDecisionTargetKind.Extraction
                ? _config.ExtractionPenaltyScale
                : 1f;
            float riskPenalty = risk * _config.RiskPenaltyWeight * penaltyScale;
            float distancePenalty = distance * _config.DistancePenaltyWeight * penaltyScale;
            float score = baseScore;
            score -= riskPenalty;
            score -= distancePenalty;

            float currentTargetBonus = 0f;
            if (candidate.IsCurrentTarget)
            {
                currentTargetBonus = _config.CurrentTargetBonus;
                score += currentTargetBonus;
            }

            float lowHealthBonus = 0f;
            if (candidate.DecisionTargetKind == AgentDecisionTargetKind.Extraction &&
                context.HealthRatio <= _config.LowHealthExtractionRatio)
            {
                lowHealthBonus = _config.LowHealthExtractionBonus;
                score += lowHealthBonus;
            }

            float carriedValueBonus = 0f;
            if (candidate.DecisionTargetKind == AgentDecisionTargetKind.Extraction)
            {
                float carriedRatio = Mathf.Clamp01(context.CarriedValue / _config.CarriedValueTarget);
                carriedValueBonus = carriedRatio * _config.CarriedValueExtractionBonus;
                score += carriedValueBonus;
            }

            return new AgentScoreBreakdown(baseScore, risk, riskPenalty, distance, distancePenalty, penaltyScale,
                currentTargetBonus, lowHealthBonus, carriedValueBonus, score);
        }

        /// <summary>评分实际使用的得分，供遥测一致性检查</summary>
        public float ScoreForTelemetryCheck(AgentDecisionContext context, AgentDecisionCandidate candidate, float risk)
            => CalculateScore(context, candidate, risk);

        /// <summary>评分实际使用的风险，供遥测一致性检查</summary>
        public float RiskForTelemetryCheck(AgentDecisionContext context, AgentDecisionCandidate candidate)
            => CalculateRisk(context, candidate);

        private AgentCandidateVerdict RejectionVerdict(AgentDecisionContext context, AgentDecisionCandidate candidate)
            => candidate.DecisionTargetKind == AgentDecisionTargetKind.ActiveEnemy && context.HealthRatio < _config.MinCombatHealthRatio
                ? AgentCandidateVerdict.LowHealthCombat
                : AgentCandidateVerdict.RiskOverThreshold;
#endif

        private static string BuildReason(
            AgentDecisionCandidate candidate,
            float score,
            float risk)
        {
            return $"{candidate.DecisionTargetKind} score={score:0.##} risk={risk:0.##}";
        }

        // 当前没有图路径风险时，先用直线路径邻近敌人估算 CL
        private static bool IsEnemyNearPath(
            Vector3 from,
            Vector3 to,
            Vector3 enemyPosition,
            float radius)
        {
            if (radius <= 0f)
                return false;

            Vector2 from2 = new Vector2(from.x, from.z);
            Vector2 to2 = new Vector2(to.x, to.z);
            Vector2 enemy2 = new Vector2(enemyPosition.x, enemyPosition.z);
            Vector2 segment = to2 - from2;

            if (segment.sqrMagnitude <= 0.0001f)
                return (enemy2 - from2).sqrMagnitude <= radius * radius;

            float t = Mathf.Clamp01(Vector2.Dot(enemy2 - from2, segment) / segment.sqrMagnitude);
            Vector2 closest = from2 + segment * t;
            return (enemy2 - closest).sqrMagnitude <= radius * radius;
        }
    }
}
