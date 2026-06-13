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

        /// <summary>
        /// 从候选目标中选择目标
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

            bool hasNearestWorkTarget = false;
            AgentDecisionCandidate nearestWorkTarget = default;
            float nearestWorkDistanceSqr = float.MaxValue;

            for (int index = 0; index < candidates.Count; index++)
            {
                AgentDecisionCandidate candidate = candidates[index];
                if (!candidate.IsValid)
                    continue;

                if (candidate.DecisionTargetKind != AgentDecisionTargetKind.ActiveEnemy &&
                    candidate.DecisionTargetKind != AgentDecisionTargetKind.Resource)
                {
                    continue;
                }

                if (hasNearestWorkTarget && candidate.DistanceSqr >= nearestWorkDistanceSqr)
                    continue;

                hasNearestWorkTarget = true;
                nearestWorkTarget = candidate;
                nearestWorkDistanceSqr = candidate.DistanceSqr;
            }

            if (hasNearestWorkTarget)
            {
                result = BuildTemporaryNearestResult(context, nearestWorkTarget);
                return true;
            }

            bool hasNearestExtraction = false;
            AgentDecisionCandidate nearestExtraction = default;
            float nearestExtractionDistanceSqr = float.MaxValue;

            for (int index = 0; index < candidates.Count; index++)
            {
                AgentDecisionCandidate candidate = candidates[index];
                if (!candidate.IsValid ||
                    candidate.DecisionTargetKind != AgentDecisionTargetKind.Extraction)
                {
                    continue;
                }

                if (hasNearestExtraction && candidate.DistanceSqr >= nearestExtractionDistanceSqr)
                    continue;

                hasNearestExtraction = true;
                nearestExtraction = candidate;
                nearestExtractionDistanceSqr = candidate.DistanceSqr;
            }

            if (hasNearestExtraction)
            {
                result = BuildTemporaryNearestResult(context, nearestExtraction);
                return true;
            }

            result = AgentDecisionResult.NoDecision("没有可用 ActiveEnemy/Resource，且没有可用撤离点");
            return false;
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
            score -= risk * _config.RiskPenaltyWeight;
            score -= distance * _config.DistancePenaltyWeight;

            if (candidate.IsCurrentTarget)
                score += _config.CurrentTargetBonus;

            if (candidate.DecisionTargetKind == AgentDecisionTargetKind.Extraction &&
                context.HealthRatio <= _config.LowHealthExtractionRatio)
            {
                score += _config.LowHealthExtractionBonus;
            }

            return score;
        }

        private AgentDecisionResult BuildTemporaryNearestResult(
            AgentDecisionContext context,
            AgentDecisionCandidate candidate)
        {
            float risk = CalculateRisk(context, candidate);
            float distance = Mathf.Sqrt(Mathf.Max(0f, candidate.DistanceSqr));
            float score = -distance;
            string reason = $"TemporaryNearest {candidate.DecisionTargetKind} distance={distance:0.##} risk={risk:0.##}";

            return new AgentDecisionResult(
                true,
                candidate,
                score,
                risk,
                reason);
        }

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
