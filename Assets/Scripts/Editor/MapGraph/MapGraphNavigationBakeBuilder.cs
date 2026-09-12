using System;
using System.Collections.Generic;
using System.Linq;
using Gameplay.MapGraph.Config;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>将已测量的矩阵映射到作者边身份和方向，不执行导航查询，不混合 profile 锚点。</summary>
    public static class MapGraphNavigationBakeBuilder
    {
        public static MapGraphNavigationBakeData Build(MapGraphGenerationResult result, long revision, string profileId)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return Build(result.Scene, result.Layout, result.Anchors, result.Connections, revision, profileId);
        }

        public static MapGraphNavigationBakeData Build(MapGraphSceneSnapshot scene, MapGraphLayoutDraft layout,
            IReadOnlyList<MapGraphScannedAnchor> anchors, IReadOnlyList<MapGraphScannedConnection> connections, long revision, string profileId)
        {
            if (scene == null || layout == null || anchors == null || connections == null) throw new ArgumentNullException();
            var profile = scene.Profiles.SingleOrDefault(p => p.Data.ProfileId == profileId);
            if (profile == null) throw new ArgumentException("UnknownBakeProfile:" + profileId);
            var matrix = connections.Where(c => c.ProfileId == profileId).Select(c => c.Edge).ToArray();
            var validation = MapGraphValidationResult.Combine(MapGraphValidation.Validate(layout), MapGraphNavigationValidation.Validate(layout, matrix));
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Issues.Where(i => i.IsError)));
            var points = new Dictionary<string, MapGraphScannedAnchor>(StringComparer.Ordinal);
            foreach (var anchor in anchors.Where(a => a.ProfileId == profileId))
            {
                if (!anchor.IsValid || !MapGraphGeometry.Finite(anchor.Position.x) || !MapGraphGeometry.Finite(anchor.Position.y) || !MapGraphGeometry.Finite(anchor.Position.z) ||
                    !layout.Graph.TryGetNode(anchor.NodeId, out _) || points.ContainsKey(anchor.NodeId))
                    throw new InvalidOperationException("InvalidBakeAnchor:" + anchor.NodeId);
                points.Add(anchor.NodeId, anchor);
            }
            if (points.Count != layout.Nodes.Count) throw new InvalidOperationException("IncompleteBakeAnchors");
            var pairs = matrix.ToDictionary(e => MapGraphGeometry.PairKey(e.FromNodeId, e.ToNodeId), StringComparer.Ordinal);
            var selected = new List<MapGraphNavigationEdgeBake>();
            foreach (var edge in layout.Edges)
            {
                var measured = pairs[MapGraphGeometry.PairKey(edge.FromNodeId, edge.ToNodeId)];
                bool forward = measured.FromNodeId == edge.FromNodeId;
                var from = forward ? measured.FromAnchor : measured.ToAnchor; var to = forward ? measured.ToAnchor : measured.FromAnchor;
                if (!from.Equals(points[edge.FromNodeId].Position) || !to.Equals(points[edge.ToNodeId].Position))
                    throw new InvalidOperationException("BakeAnchorMismatch:" + edge.EdgeId);
                selected.Add(new MapGraphNavigationEdgeBake(edge.EdgeId, edge.FromNodeId, edge.ToNodeId, from, to,
                    forward ? measured.ForwardLength : measured.ReverseLength, forward ? measured.ReverseLength : measured.ForwardLength));
            }
            return new MapGraphNavigationBakeData(scene.SceneFingerprint, scene.NavigationFingerprint, revision, profile.Data, selected);
        }
    }
}
