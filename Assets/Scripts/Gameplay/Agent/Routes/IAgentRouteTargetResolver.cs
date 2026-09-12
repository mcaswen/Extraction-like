using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace Gameplay.Agent.Routes
{
    public enum AgentRouteTargetStatus { Missing, Unavailable, WaitingSpawn, Ready, Completed, SpawnFailed }

    /// <summary>群的世界事实；完成不等于不可通行，出生未就绪不等于清群。</summary>
    public readonly struct AgentRouteTargetFacts
    {
        public string NodeId { get; }
        public MapGraphNodeKind Kind { get; }
        public Vector3 Anchor { get; }
        public AgentRouteTargetStatus Status { get; }
        public int AliveMembers { get; }
        public bool CanTraverse => Status != AgentRouteTargetStatus.Missing && Status != AgentRouteTargetStatus.Unavailable &&
            Finite(Anchor.x) && Finite(Anchor.y) && Finite(Anchor.z);
        public AgentRouteTargetFacts(string nodeId, MapGraphNodeKind kind, Vector3 anchor,
            AgentRouteTargetStatus status, int aliveMembers = 0)
        { NodeId = nodeId; Kind = kind; Anchor = anchor; Status = status; AliveMembers = aliveMembers; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>由世界绑定实现。只解析身份、锚点、成员事实和单次候选，不拥有路线或挂起任务。</summary>
    public interface IAgentRouteTargetResolver
    {
        // 配置/锚点/绑定修订；成员死亡不应使整张图的导航版本失效。
        long Revision { get; }
        bool TryResolveNode(AgentTargetRef target, out string nodeId);
        bool TryGetFacts(string nodeId, out AgentRouteTargetFacts facts);
        bool TryCreateProcessingDirective(string nodeId, AgentId agentId, string commandId, int priority,
            out AgentDirectiveRequest directive, out AgentDirectiveFailure failure);
    }
}
