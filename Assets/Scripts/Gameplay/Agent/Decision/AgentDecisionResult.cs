namespace Gameplay.Agent.Decision
{
    /// <summary>
    /// Agent 决策评分结果
    /// 用于发现层投递指令，也用于写回黑板调试信息
    /// </summary>
    public readonly struct AgentDecisionResult
    {
        public AgentDecisionResult(
            bool hasDecision,
            AgentDecisionCandidate candidate,
            float score,
            float risk,
            string reason)
        {
            HasDecision = hasDecision;
            Candidate = candidate;
            Score = score;
            Risk = risk;
            Reason = reason ?? string.Empty;
        }

        public bool HasDecision { get; }
        public AgentDecisionCandidate Candidate { get; }
        public float Score { get; }
        public float Risk { get; }
        public string Reason { get; }

        public static AgentDecisionResult NoDecision(string reason)
        {
            return new AgentDecisionResult(
                false,
                default,
                0f,
                0f,
                reason);
        }
    }
}
