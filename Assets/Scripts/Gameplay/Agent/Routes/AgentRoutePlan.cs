using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Agent.Routes
{
    /// <summary>规划完成的冻结结果；包含仍需到达的入口，不持有执行游标。</summary>
    public sealed class AgentRoutePlan
    {
        public IReadOnlyList<string> NodeIds { get; }
        public string EntryNodeId => NodeIds[0];
        public string TargetNodeId => NodeIds[NodeIds.Count - 1];
        public Vector3 Origin { get; }
        public float EntryLength { get; }
        public float GraphLength { get; }
        public long GraphRevision { get; }
        public long CostRevision { get; }
        public long BindingRevision { get; }
        public string ProfileId { get; }

        internal AgentRoutePlan(IReadOnlyList<string> nodes, Vector3 origin, float entryLength, float graphLength,
            long graphRevision, long costRevision, long bindingRevision, string profileId)
        {
            var copy = new List<string>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++) copy.Add(nodes[i]);
            NodeIds = copy.AsReadOnly(); Origin = origin; EntryLength = entryLength; GraphLength = graphLength;
            GraphRevision = graphRevision; CostRevision = costRevision; BindingRevision = bindingRevision; ProfileId = profileId;
        }
    }
}
