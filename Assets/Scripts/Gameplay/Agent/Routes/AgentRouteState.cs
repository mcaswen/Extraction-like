namespace Gameplay.Agent.Routes
{
    /// <summary>只由一个 RouteController 修改，保存根序列，不保存受击挂起指令。</summary>
    internal sealed class AgentRouteState
    {
        public AgentRouteRequest Request;
        public long Version;
        public AgentRoutePlan Plan;
        public AgentRouteEnvironment Environment;
        public int Cursor;
        public int ReplanCount;
        public bool SkipResources;
        public AgentRouteStage Stage;
        public AgentRouteFailure Failure;
        public bool IsActive => Stage == AgentRouteStage.Accepted;
    }
}
