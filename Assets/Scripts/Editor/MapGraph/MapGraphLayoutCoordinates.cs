using System;
using System.Collections.Generic;
using Gameplay.MapGraph.Config;
using UnityEngine;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>固定方向后的数值约束工作区，X/Y 对齐组独立，不拥有拓扑生成或导航成本。</summary>
    internal sealed class MapGraphLayoutCoordinates
    {
        private readonly MapGraphLayoutDraft _reference;
        private readonly MapGraphGenerationSettings _settings;
        private readonly MapGraphEdgeDefinition[] _edges;
        private readonly int[] _from, _to, _directions;
        private readonly AxisCoordinates _x, _y;
        private readonly List<int>[] _members;
        private readonly Vector2[] _emptyOffsets;
        private readonly Dictionary<string, MapGraphAlignmentConstraint> _lines = new Dictionary<string, MapGraphAlignmentConstraint>();
        private readonly Vector2[] _points;
        internal string Failure { get; private set; }

        internal MapGraphLayoutCoordinates(MapGraphLayoutDraft reference, MapGraphEdgeDefinition[] edges, int[] from, int[] to,
            int[] directions, MapGraphGenerationSettings settings)
        {
            _reference = reference; _edges = edges; _from = from; _to = to; _directions = directions; _settings = settings;
            _x = new AxisCoordinates(reference.Nodes.Count); _y = new AxisCoordinates(reference.Nodes.Count);
            _points = new Vector2[reference.Nodes.Count];
            _members = new List<int>[reference.Zones.Count]; _emptyOffsets = new Vector2[reference.Zones.Count];
            for (int z = 0; z < _members.Length; z++)
            {
                _members[z] = new List<int>();
                for (int n = 0; n < reference.Nodes.Count; n++) if (reference.Nodes[n].ZoneId == reference.Zones[z].ZoneId) _members[z].Add(n);
            }
            foreach (var line in reference.Constraints.Alignments) if (line != null) _lines[line.Id] = line;
        }

        internal bool TrySolve(out MapGraphLayoutDraft draft)
        {
            draft = null;
            for (int e = 0; e < _edges.Length; e++)
                if (_directions[e] < 2) _y.Merge(_from[e], _to[e]); else _x.Merge(_from[e], _to[e]);
            if (!Initialize(_x, true) || !Initialize(_y, false)) return false;
            for (int i = 0; i < _points.Length; i++)
                for (int j = i + 1; j < _points.Length; j++)
                    if (_x.Group(i) == _x.Group(j) && _y.Group(i) == _y.Group(j)) return Fail("CoincidentAlignmentGroups");
            var zones = new MapGraphZoneDefinition[_members.Length];
            var edges = new MapGraphEdgeDefinition[_edges.Length];
            for (int e = 0; e < edges.Length; e++)
            {
                var source = _edges[e];
                edges[e] = source.WithPresentation(_directions[e] < 2 ? MapGraphAxis.Horizontal : MapGraphAxis.Vertical,
                    source.FromInset, source.ToInset, source.WidthOverride, source.UseColorOverride, source.ColorOverride, source.Origin);
            }
            for (int iteration = 0; iteration < _settings.MaximumLayoutIterations; iteration++)
            {
                for (int e = 0; e < edges.Length; e++)
                {
                    int a = _from[e], b = _to[e]; bool horizontal = _directions[e] < 2;
                    float firstSize = horizontal ? _reference.Nodes[a].Footprint.x : _reference.Nodes[a].Footprint.y;
                    float secondSize = horizontal ? _reference.Nodes[b].Footprint.x : _reference.Nodes[b].Footprint.y;
                    float gap = Mathf.Max(_settings.GridSpacing, (firstSize + secondSize) * 0.5f + edges[e].FromInset + edges[e].ToInset + 8);
                    var axis = horizontal ? _x : _y;
                    if ((_directions[e] & 1) != 0) (a, b) = (b, a);
                    if (!axis.Separate(a, b, gap)) return Fail("FixedEdgeGapConflict:" + edges[e].EdgeId);
                }
                if (!SeparateNodes() || !ConstrainLockedZones()) return false;
                ReadPositions();
                var segments = new (Vector2 from, Vector2 to, float width)[edges.Length];
                for (int e = 0; e < edges.Length; e++)
                    segments[e] = (_points[_from[e]], _points[_to[e]], edges[e].WidthOverride > 0 ? edges[e].WidthOverride : 2);
                bool retry = false;
                for (int z = 0; z < zones.Length; z++)
                {
                    var original = _reference.Zones[z];
                    if (_members[z].Count == 0 && !original.LayoutLocked)
                        original = original.WithLayout(new Rect(original.Bounds.position + _emptyOffsets[z], original.Bounds.size), false);
                    if (MapGraphZoneLayout.TryFit(original, _reference.Nodes, _points, _members[z], segments, _settings, out zones[z])) continue;
                    if (_members[z].Count == 0 && !original.LayoutLocked)
                    { _emptyOffsets[z] += Vector2.up * _settings.GridSpacing; retry = true; break; }
                    return Fail("ZoneNameOrContainmentConflict:" + original.ZoneId);
                }
                if (retry) continue;
                for (int a = 0; a < zones.Length; a++)
                    for (int b = a + 1; b < zones.Length; b++)
                        if (MapGraphGeometry.Overlaps(zones[a].Bounds, zones[b].Bounds))
                        {
                            if (!SeparateZones(a, b, zones[a].Bounds, zones[b].Bounds)) return Fail("ZoneSeparationConflict:" + zones[a].ZoneId + ":" + zones[b].ZoneId);
                            retry = true;
                        }
                if (retry) continue;
                var candidate = MapGraphLayoutGenerator.FromCoordinates(_reference, _points, zones, edges, _x.Groups(), _y.Groups());
                var validation = MapGraphValidation.Validate(candidate, _reference);
                if (validation.IsValid) { draft = candidate; return true; }
                Failure = validation.Issues[0].ToString();
            }
            return Fail("CoordinateBudgetExhausted:" + Failure);
        }

        private bool Initialize(AxisCoordinates axis, bool horizontalCoordinate)
        {
            var values = new float[_points.Length]; var fixedValues = new bool[_points.Length];
            for (int i = 0; i < values.Length; i++)
            {
                var node = _reference.Nodes[i]; var point = _reference.Graph.GetNodePosition(node.NodeId);
                values[i] = horizontalCoordinate ? point.x : point.y; fixedValues[i] = node.PositionLocked;
                string lineId = horizontalCoordinate ? node.ColumnId : node.RowId;
                if (_lines.TryGetValue(lineId, out var line) && line.Locked)
                {
                    if (fixedValues[i] && !MapGraphGeometry.Near(values[i], line.Coordinate)) return Fail("LockedPointAlignmentConflict:" + node.NodeId);
                    values[i] = line.Coordinate; fixedValues[i] = true;
                }
            }
            return axis.Initialize(values, fixedValues) || Fail("LockedAlignmentConflict");
        }

        private bool SeparateNodes()
        {
            for (int a = 0; a < _points.Length; a++)
                for (int b = a + 1; b < _points.Length; b++)
                {
                    Vector2 size = (_reference.Nodes[a].Footprint + _reference.Nodes[b].Footprint) * 0.5f + Vector2.one * 12;
                    float dx = _x.Get(b) - _x.Get(a), dy = _y.Get(b) - _y.Get(a);
                    float xGap = size.x - Mathf.Abs(dx), yGap = size.y - Mathf.Abs(dy);
                    if (xGap <= 0 || yGap <= 0) continue;
                    bool canX = _x.CanSeparate(a, b), canY = _y.CanSeparate(a, b);
                    if (!canX && !canY) return Fail("LockedNodeOverlap:" + _reference.Nodes[a].NodeId + ":" + _reference.Nodes[b].NodeId);
                    bool horizontal = canX && (!canY || xGap <= yGap);
                    float delta = horizontal ? dx : dy;
                    if (Mathf.Abs(delta) <= MapGraphGeometry.Epsilon)
                    {
                        Vector2 original = _reference.Graph.GetNodePosition(_reference.Nodes[b].NodeId) - _reference.Graph.GetNodePosition(_reference.Nodes[a].NodeId);
                        delta = horizontal ? original.x : original.y;
                        if (Mathf.Abs(delta) <= MapGraphGeometry.Epsilon) delta = string.CompareOrdinal(_reference.Nodes[a].NodeId, _reference.Nodes[b].NodeId) < 0 ? 1 : -1;
                    }
                    var axis = horizontal ? _x : _y;
                    if (!axis.Separate(delta > 0 ? a : b, delta > 0 ? b : a, horizontal ? size.x : size.y)) return Fail("NodeSeparationConflict");
                }
            return true;
        }

        private bool ConstrainLockedZones()
        {
            for (int z = 0; z < _members.Length; z++)
            {
                var zone = _reference.Zones[z]; if (!zone.LayoutLocked) continue;
                foreach (int node in _members[z])
                {
                    Vector2 half = _reference.Nodes[node].Footprint * 0.5f;
                    if (!_x.Clamp(node, zone.Bounds.xMin + half.x, zone.Bounds.xMax - half.x) ||
                        !_y.Clamp(node, zone.Bounds.yMin + half.y, zone.Bounds.yMax - half.y)) return Fail("LockedZoneContainment:" + zone.ZoneId);
                }
            }
            return true;
        }

        private bool SeparateZones(int a, int b, Rect first, Rect second)
        {
            bool shareX = _x.Share(_members[a], _members[b]), shareY = _y.Share(_members[a], _members[b]);
            bool moveAX = CanMoveZone(a, _x), moveBX = CanMoveZone(b, _x), moveAY = CanMoveZone(a, _y), moveBY = CanMoveZone(b, _y);
            bool canX = !shareX && (moveAX || moveBX), canY = !shareY && (moveAY || moveBY);
            if (!canX && !canY) return false;
            Vector2 direction = _reference.Zones[b].Bounds.center - _reference.Zones[a].Bounds.center;
            if (Mathf.Abs(direction.x) <= MapGraphGeometry.Epsilon) direction.x = second.center.x - first.center.x;
            if (Mathf.Abs(direction.y) <= MapGraphGeometry.Epsilon) direction.y = second.center.y - first.center.y;
            float signX = direction.x < 0 ? -1 : 1, signY = direction.y < 0 ? -1 : 1;
            float xShift = (signX > 0 ? first.xMax - second.xMin : second.xMax - first.xMin) + _settings.ZonePadding;
            float yShift = (signY > 0 ? first.yMax - second.yMin : second.yMax - first.yMin) + _settings.ZonePadding;
            bool horizontal = canX && (!canY || xShift <= yShift);
            float delta = horizontal ? xShift * signX : yShift * signY;
            bool moveA = horizontal ? moveAX : moveAY, moveB = horizontal ? moveBX : moveBY;
            if (moveA) ShiftZone(a, horizontal, -delta * (moveB ? 0.5f : 1));
            if (moveB) ShiftZone(b, horizontal, delta * (moveA ? 0.5f : 1));
            return true;
        }
        private bool CanMoveZone(int zone, AxisCoordinates axis)
            => !_reference.Zones[zone].LayoutLocked && axis.CanMove(_members[zone]);
        private void ShiftZone(int zone, bool horizontal, float amount)
        {
            if (_members[zone].Count == 0) _emptyOffsets[zone] += (horizontal ? Vector2.right : Vector2.up) * amount;
            else (horizontal ? _x : _y).Shift(_members[zone], amount);
        }
        private void ReadPositions() { for (int i = 0; i < _points.Length; i++) _points[i] = new Vector2(_x.Get(i), _y.Get(i)); }
        private bool Fail(string reason) { Failure = reason; return false; }

        private sealed class AxisCoordinates
        {
            private readonly int[] _parent;
            private readonly float[] _values;
            private readonly bool[] _fixed;
            internal AxisCoordinates(int count)
            { _parent = new int[count]; _values = new float[count]; _fixed = new bool[count]; for (int i = 0; i < count; i++) _parent[i] = i; }
            internal int Group(int node) { while (_parent[node] != node) node = _parent[node]; return node; }
            internal void Merge(int a, int b) { a = Group(a); b = Group(b); if (a != b) _parent[Math.Max(a, b)] = Math.Min(a, b); }
            internal int[] Groups() { var result = new int[_parent.Length]; for (int i = 0; i < result.Length; i++) result[i] = Group(i); return result; }
            internal bool Initialize(float[] initial, bool[] locked)
            {
                var counts = new int[initial.Length]; var sums = new double[initial.Length];
                for (int i = 0; i < initial.Length; i++)
                {
                    if (!MapGraphGeometry.Finite(initial[i])) return false;
                    int root = Group(i); counts[root]++; sums[root] += initial[i];
                    if (!locked[i]) continue;
                    if (_fixed[root] && !MapGraphGeometry.Near(_values[root], initial[i])) return false;
                    _fixed[root] = true; _values[root] = initial[i];
                }
                for (int i = 0; i < initial.Length; i++) if (counts[i] > 0 && !_fixed[i]) _values[i] = (float)(sums[i] / counts[i]);
                return true;
            }
            internal float Get(int node) => _values[Group(node)];
            internal bool CanSeparate(int a, int b) => Group(a) != Group(b) && (!_fixed[Group(a)] || !_fixed[Group(b)]);
            internal bool Separate(int lower, int upper, float gap)
            {
                lower = Group(lower); upper = Group(upper);
                float missing = gap - (_values[upper] - _values[lower]);
                if (missing <= MapGraphGeometry.Epsilon) return true;
                if (lower == upper || _fixed[lower] && _fixed[upper]) return false;
                if (!_fixed[lower]) _values[lower] -= missing * (_fixed[upper] ? 1 : 0.5f);
                if (!_fixed[upper]) _values[upper] += missing * (_fixed[lower] ? 1 : 0.5f);
                return true;
            }
            internal bool Clamp(int node, float min, float max)
            {
                if (min > max) return false;
                int root = Group(node); float value = Mathf.Clamp(_values[root], min, max);
                if (_fixed[root] && !MapGraphGeometry.Near(value, _values[root])) return false;
                _values[root] = value; return true;
            }
            internal bool CanMove(List<int> nodes) { foreach (int node in nodes) if (_fixed[Group(node)]) return false; return true; }
            internal bool Share(List<int> a, List<int> b)
            { foreach (int x in a) foreach (int y in b) if (Group(x) == Group(y)) return true; return false; }
            internal void Shift(List<int> nodes, float amount)
            {
                var seen = new bool[_parent.Length];
                foreach (int node in nodes) { int root = Group(node); if (!seen[root]) { _values[root] += amount; seen[root] = true; } }
            }
        }
    }
}
