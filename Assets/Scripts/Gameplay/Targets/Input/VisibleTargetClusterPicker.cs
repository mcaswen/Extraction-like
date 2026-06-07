using System.Collections.Generic;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using UnityEngine;

namespace Gameplay.Targets.Input
{
    /// <summary>
    /// Picks currently camera-visible target clusters by projecting their range outline to screen space.
    /// </summary>
    public sealed class VisibleTargetClusterPicker
    {
        private const int MinimumPolygonPointCount = 3;
        private const float ScreenEdgePadding = 16f;

        private readonly List<GameplayTargetClusterAuthoringBase> _clusterBuffer =
            new List<GameplayTargetClusterAuthoringBase>();

        private readonly List<Vector3> _worldPointBuffer = new List<Vector3>();
        private readonly List<Vector2> _screenPointBuffer = new List<Vector2>();

        public bool TryPick(
            Camera camera,
            Vector2 screenPosition,
            TargetClusterPickOptions options,
            out GameplayTargetClusterAuthoringBase selectedCluster)
        {
            selectedCluster = null;
            if (camera == null)
                return false;

            float selectedDepth = float.PositiveInfinity;
            GameplayTargetRegistry registry = GameplayTargetRegistry.GetOrCreate();
            registry.CopyClustersTo(_clusterBuffer);

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                GameplayTargetClusterAuthoringBase cluster = _clusterBuffer[i];
                if (!CanConsiderCluster(cluster, options))
                    continue;

                if (!TryBuildScreenPolygon(camera, cluster, _screenPointBuffer, out float averageDepth))
                    continue;

                if (!IsScreenPolygonVisible(camera, _screenPointBuffer))
                    continue;

                if (!IsPointInsidePolygon(screenPosition, _screenPointBuffer))
                    continue;

                if (averageDepth >= selectedDepth)
                    continue;

                selectedCluster = cluster;
                selectedDepth = averageDepth;
            }

            return selectedCluster != null;
        }

        private static bool CanConsiderCluster(
            GameplayTargetClusterAuthoringBase cluster,
            TargetClusterPickOptions options)
        {
            if (cluster == null || !cluster.isActiveAndEnabled)
                return false;

            if (options.IgnoreCompletedClusters && cluster.HasBeenCompleted)
                return false;

            if (!options.UseClusterLayerMask)
                return true;

            int clusterLayerMask = 1 << cluster.gameObject.layer;
            return (options.ClusterLayerMask.value & clusterLayerMask) != 0;
        }

        private bool TryBuildScreenPolygon(
            Camera camera,
            GameplayTargetClusterAuthoringBase cluster,
            List<Vector2> screenPoints,
            out float averageDepth)
        {
            screenPoints.Clear();
            averageDepth = 0f;

            FillWorldPoints(cluster, _worldPointBuffer);
            if (_worldPointBuffer.Count < MinimumPolygonPointCount)
                return false;

            for (int i = 0; i < _worldPointBuffer.Count; i++)
            {
                Vector3 screenPoint = camera.WorldToScreenPoint(_worldPointBuffer[i]);
                if (screenPoint.z <= camera.nearClipPlane)
                    return false;

                averageDepth += screenPoint.z;
                screenPoints.Add(new Vector2(screenPoint.x, screenPoint.y));
            }

            if (screenPoints.Count < MinimumPolygonPointCount)
                return false;

            averageDepth /= screenPoints.Count;
            return true;
        }

        private static void FillWorldPoints(
            GameplayTargetClusterAuthoringBase cluster,
            List<Vector3> worldPoints)
        {
            worldPoints.Clear();

            LineRenderer lineRenderer = cluster.GetComponent<LineRenderer>();
            if (lineRenderer != null && lineRenderer.positionCount >= MinimumPolygonPointCount)
            {
                for (int i = 0; i < lineRenderer.positionCount; i++)
                {
                    Vector3 point = lineRenderer.GetPosition(i);
                    worldPoints.Add(lineRenderer.useWorldSpace ? point : lineRenderer.transform.TransformPoint(point));
                }

                return;
            }

            cluster.RefreshRangeShape();

            IReadOnlyList<Vector3> rangePoints = cluster.RangePoints;
            if (rangePoints == null)
                return;

            for (int i = 0; i < rangePoints.Count; i++)
                worldPoints.Add(rangePoints[i]);
        }

        private static bool IsScreenPolygonVisible(
            Camera camera,
            IReadOnlyList<Vector2> screenPoints)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < screenPoints.Count; i++)
            {
                Vector2 point = screenPoints[i];
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            Rect cameraRect = camera.pixelRect;
            return maxX >= cameraRect.xMin - ScreenEdgePadding &&
                   maxY >= cameraRect.yMin - ScreenEdgePadding &&
                   minX <= cameraRect.xMax + ScreenEdgePadding &&
                   minY <= cameraRect.yMax + ScreenEdgePadding;
        }

        private static bool IsPointInsidePolygon(
            Vector2 point,
            IReadOnlyList<Vector2> polygon)
        {
            bool inside = false;
            int previousIndex = polygon.Count - 1;

            for (int currentIndex = 0; currentIndex < polygon.Count; currentIndex++)
            {
                Vector2 current = polygon[currentIndex];
                Vector2 previous = polygon[previousIndex];

                bool crossesHorizontalLine = (current.y > point.y) != (previous.y > point.y);
                if (crossesHorizontalLine)
                {
                    float deltaY = previous.y - current.y;
                    if (Mathf.Abs(deltaY) > Mathf.Epsilon)
                    {
                        float intersectionX =
                            (previous.x - current.x) * (point.y - current.y) / deltaY + current.x;

                        if (point.x < intersectionX)
                            inside = !inside;
                    }
                }

                previousIndex = currentIndex;
            }

            return inside;
        }
    }
}
