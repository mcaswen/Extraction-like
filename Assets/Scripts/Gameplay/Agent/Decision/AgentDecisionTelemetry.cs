#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Gameplay.Agent.Decision
{
    /// <summary>评分候选的判定结果</summary>
    public enum AgentCandidateVerdict
    {
        Invalid = 0,
        LowHealthCombat = 1,
        RiskOverThreshold = 2,
        NotBest = 3,
        Chosen = 4
    }

    /// <summary>决策刷新没有进入评分的原因</summary>
    public enum AgentDecisionSkipReason
    {
        None = 0,
        RouteHeld = 1,
        ManualDirectiveLock = 2,
        RetaliationLock = 3,
        ModuleDisabled = 4,
        NoDiscoveryRange = 5
    }

    /// <summary>
    /// 单个候选的得分拆项，按与评分相同的运算顺序重算，Total 与实际得分逐位一致
    /// </summary>
    public readonly struct AgentScoreBreakdown
    {
        public readonly float BaseScore;
        public readonly float Risk;
        public readonly float RiskPenalty;
        public readonly float Distance;
        public readonly float DistancePenalty;
        public readonly float PenaltyScale;
        public readonly float CurrentTargetBonus;
        public readonly float LowHealthBonus;
        public readonly float CarriedValueBonus;
        public readonly float Total;

        public AgentScoreBreakdown(float baseScore, float risk, float riskPenalty, float distance, float distancePenalty,
            float penaltyScale, float currentTargetBonus, float lowHealthBonus, float carriedValueBonus, float total)
        {
            BaseScore = baseScore;
            Risk = risk;
            RiskPenalty = riskPenalty;
            Distance = distance;
            DistancePenalty = distancePenalty;
            PenaltyScale = penaltyScale;
            CurrentTargetBonus = currentTargetBonus;
            LowHealthBonus = lowHealthBonus;
            CarriedValueBonus = carriedValueBonus;
            Total = total;
        }
    }

    /// <summary>
    /// 决策评分的遥测接收器：只接收评分本来就算出的数，不影响候选的取舍
    /// </summary>
    public interface IAgentDecisionRoundSink
    {
        void BeginRound(double timeSeconds, in AgentDecisionContext context);

        void BeginList(bool fallbackExits);

        void RecordCandidate(int index, in AgentDecisionCandidate candidate, AgentCandidateVerdict verdict, in AgentScoreBreakdown breakdown);

        void EndList(int bestIndex);

        void RecordSkip(double timeSeconds, AgentDecisionSkipReason reason);
    }
}
#endif
