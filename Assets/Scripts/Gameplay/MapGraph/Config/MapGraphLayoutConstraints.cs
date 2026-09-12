using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.MapGraph.Config
{
    /// <summary>每条边只允许一根横线或竖线；Unspecified 仅供历史图兼容。</summary>
    public enum MapGraphAxis { Unspecified = 0, Horizontal = 1, Vertical = 2 }
    public enum MapGraphEdgeOrigin { Legacy = 0, Generated = 1, Manual = 2 }

    /// <summary>行锁定 Y，列锁定 X；坐标属于全图，不能按各 Zone 独立漂移。</summary>
    [Serializable]
    public sealed class MapGraphAlignmentConstraint
    {
        [SerializeField] private string _id;
        [SerializeField] private MapGraphAxis _axis;
        [SerializeField] private float _coordinate;
        [SerializeField] private bool _locked;
        public string Id => _id ?? string.Empty;
        public MapGraphAxis Axis => _axis;
        public float Coordinate => _coordinate;
        public bool Locked => _locked;
        public MapGraphAlignmentConstraint(string id, MapGraphAxis axis, float coordinate, bool locked = false)
        { _id = id; _axis = axis; _coordinate = coordinate; _locked = locked; }
    }

    /// <summary>人工删除的无向连接，重新生成不能把它补回来；它本身不是可走的边。</summary>
    [Serializable]
    public sealed class MapGraphConnectionExclusion
    {
        [SerializeField] private string _firstNodeId;
        [SerializeField] private string _secondNodeId;
        public string FirstNodeId => _firstNodeId ?? string.Empty;
        public string SecondNodeId => _secondNodeId ?? string.Empty;
        public MapGraphConnectionExclusion(string firstNodeId, string secondNodeId)
        {
            bool ordered = string.CompareOrdinal(firstNodeId, secondNodeId) <= 0;
            _firstNodeId = ordered ? firstNodeId : secondNodeId;
            _secondNodeId = ordered ? secondNodeId : firstNodeId;
        }
        public bool Matches(string a, string b)
            => (FirstNodeId == a && SecondNodeId == b) || (FirstNodeId == b && SecondNodeId == a);
    }

    /// <summary>持久化对齐和禁连规则，实际约束求解由 Editor 层负责。</summary>
    [Serializable]
    public sealed class MapGraphLayoutConstraints : ISerializationCallbackReceiver
    {
        [SerializeField] private List<MapGraphAlignmentConstraint> _alignments = new List<MapGraphAlignmentConstraint>();
        [SerializeField] private List<MapGraphConnectionExclusion> _excludedConnections = new List<MapGraphConnectionExclusion>();
        [NonSerialized] private IReadOnlyList<MapGraphAlignmentConstraint> _alignmentView;
        [NonSerialized] private IReadOnlyList<MapGraphConnectionExclusion> _exclusionView;
        public IReadOnlyList<MapGraphAlignmentConstraint> Alignments => _alignmentView ??= _alignments.AsReadOnly();
        public IReadOnlyList<MapGraphConnectionExclusion> ExcludedConnections => _exclusionView ??= _excludedConnections.AsReadOnly();

        public MapGraphLayoutConstraints() { }
        public MapGraphLayoutConstraints(IEnumerable<MapGraphAlignmentConstraint> alignments,
            IEnumerable<MapGraphConnectionExclusion> excludedConnections)
        {
            if (alignments != null) _alignments = new List<MapGraphAlignmentConstraint>(alignments);
            if (excludedConnections != null) _excludedConnections = new List<MapGraphConnectionExclusion>(excludedConnections);
        }
        public bool IsExcluded(string a, string b)
        {
            for (int i = 0; i < _excludedConnections.Count; i++)
                if (_excludedConnections[i] != null && _excludedConnections[i].Matches(a, b)) return true;
            return false;
        }
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() { _alignmentView = null; _exclusionView = null; }
    }
}
