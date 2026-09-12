using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Runtime;

namespace Gameplay.Agent.Routes
{
    /// <summary>安装器提供的冻结规划输入；动态世界通过只读 Resolver 观察。</summary>
    public sealed class AgentRouteEnvironment
    {
        public MapGraphService Graph { get; }
        public long GraphRevision { get; }
        public long ContextVersion { get; }
        public MapGraphCostSnapshot Costs { get; }
        public IAgentRouteTargetResolver Targets { get; }
        public AgentNavigationProfile Profile { get; }
        public long TargetRevision { get; }
        public bool NavigationReady { get; }
        public bool IsReady { get; }
        public AgentRouteEnvironment(MapGraphService graph, long graphRevision, long contextVersion,
            MapGraphCostSnapshot costs, IAgentRouteTargetResolver targets, AgentNavigationProfile profile, bool ready = true, bool navigationReady = true)
        {
            Graph = graph; GraphRevision = graphRevision; ContextVersion = contextVersion;
            Costs = costs; Targets = targets; Profile = profile;
            TargetRevision = targets?.Revision ?? 0; NavigationReady = navigationReady;
            IsReady = ready && navigationReady && graph != null && graph.IsValid && costs != null && targets != null && profile != null;
        }
    }
}
