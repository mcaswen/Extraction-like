namespace Gameplay.Agent.Navigation
{
    /// <summary>完整路径向原生 Agent 交付时的有限恢复窗口，不负责空间可达性或执行移动。</summary>
    public sealed class AgentPathAssignmentBudget
    {
        private readonly double _timeout;
        private double _firstFailure = -1;
        public AgentPathAssignmentBudget(double timeout) { _timeout = System.Math.Max(0, timeout); }
        public void Reset() { _firstFailure = -1; }
        public AgentNavigationStatus Observe(bool assigned, double gameTime)
        {
            if (assigned) { Reset(); return AgentNavigationStatus.Moving; }
            if (_firstFailure < 0) _firstFailure = gameTime;
            return gameTime - _firstFailure >= _timeout ? AgentNavigationStatus.Unreachable : AgentNavigationStatus.NotReady;
        }
    }
}
