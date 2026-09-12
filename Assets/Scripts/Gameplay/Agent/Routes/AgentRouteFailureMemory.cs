using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Runtime;
using UnityEngine;

namespace Gameplay.Agent.Routes
{
    /// <summary>每名 Agent 的根失败记忆。只影响自主候选，不持有路线或世界对象。</summary>
    public sealed class AgentRouteFailureMemory
    {
        private readonly struct Entry
        {
            public readonly MapGraphService Graph;
            public readonly IAgentRouteTargetResolver Targets;
            public readonly string ProfileId;
            public readonly long CostRevision, TargetRevision, Order;
            public readonly Vector3 Position;
            public readonly double Until;
            public Entry(AgentRouteEnvironment environment, Vector3 position, double until, long order)
            {
                Graph=environment.Graph; Targets=environment.Targets; ProfileId=environment.Costs.ProfileId;
                CostRevision=environment.Costs.Revision; TargetRevision=environment.TargetRevision;
                Position=position; Until=until; Order=order;
            }
            public bool Matches(AgentRouteEnvironment environment, Vector3 position, double now) =>
                now<Until && ReferenceEquals(Graph,environment.Graph) && ReferenceEquals(Targets,environment.Targets) &&
                ProfileId==environment.Costs.ProfileId && CostRevision==environment.Costs.Revision &&
                TargetRevision==environment.TargetRevision && (Position-position).sqrMagnitude<=4f;
        }
        private readonly Dictionary<string,Entry> _entries=new Dictionary<string,Entry>(StringComparer.Ordinal);
        private long _order;
        public int Count => _entries.Count;
        public bool CanSelect(string nodeId, AgentRouteEnvironment environment, Vector3 position, double now)
        {
            if (environment?.IsReady!=true || environment.TargetRevision!=environment.Targets.Revision) return false;
            if (!_entries.TryGetValue(nodeId,out var entry)) return true;
            if (entry.Matches(environment,position,now)) return false;
            _entries.Remove(nodeId); return true;
        }
        public void Observe(AgentRouteResult result, AgentRouteEnvironment environment, Vector3 position, double now)
        {
            string node=result.Request.TargetNodeId;
            if (string.IsNullOrEmpty(node)) return;
            if (result.Stage==AgentRouteStage.Accepted) { _entries.Remove(node); return; }
            if (result.Request.Source!=AgentRouteSource.Autonomous || environment?.Costs==null ||
                (result.Stage!=AgentRouteStage.Rejected && result.Stage!=AgentRouteStage.Failed)) return;
            double until;
            switch(result.Reason) {
                case AgentRouteFailure.Disconnected:
                case AgentRouteFailure.NoReachableEntry:
                case AgentRouteFailure.Unreachable:
                case AgentRouteFailure.NavigationNotReady: until=double.PositiveInfinity; break;
                case AgentRouteFailure.NoProgress:
                case AgentRouteFailure.SpawnFailed:
                case AgentRouteFailure.NoExecutableMember:
                case AgentRouteFailure.StaleContext:
                case AgentRouteFailure.TargetUnavailable: until=now+3; break;
                default:return;
            }
            if (_entries.Count>=64 && !_entries.ContainsKey(node)) {
                string oldest=null; long order=long.MaxValue;
                foreach(var pair in _entries) if(pair.Value.Order<order) { oldest=pair.Key; order=pair.Value.Order; }
                if(oldest!=null) _entries.Remove(oldest);
            }
            _entries[node]=new Entry(environment,position,until,++_order);
        }
    }
}
