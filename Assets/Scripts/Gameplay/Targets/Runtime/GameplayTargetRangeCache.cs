using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Targets.Runtime
{
    /// <summary>Per-owner geometry/projection cache. It does not own gameplay state or spatial queries.</summary>
    public sealed class GameplayTargetRangeCache
    {
        private static readonly Unity.Profiling.ProfilerMarker GeometryMarker = new Unity.Profiling.ProfilerMarker("Anomaly.Range.Geometry");
        private static readonly Unity.Profiling.ProfilerMarker ProjectionMarker = new Unity.Profiling.ProfilerMarker("Anomaly.Range.Projection");
        private static readonly Unity.Profiling.ProfilerMarker LineMarker = new Unity.Profiling.ProfilerMarker("Anomaly.Range.Line");
        public struct Settings
        {
            public float Padding, Radius, HeightOffset, ProbeHeight, ProbeDistance, MinNormalY;
            public int CircleSegments, SmoothSegments;
            public bool Equals(Settings other) => Padding == other.Padding && Radius == other.Radius &&
                HeightOffset == other.HeightOffset && ProbeHeight == other.ProbeHeight &&
                ProbeDistance == other.ProbeDistance && MinNormalY == other.MinNormalY &&
                CircleSegments == other.CircleSegments && SmoothSegments == other.SmoothSegments;
        }

        private readonly List<Vector3> _inputs = new List<Vector3>();
        private readonly List<Vector3> _unprojected = new List<Vector3>();
        private readonly List<Vector3> _points = new List<Vector3>();
        private readonly GameplayTargetShapeUtility.HullBuffer _hull = new GameplayTargetShapeUtility.HullBuffer();
        private Settings _settings;
        private Vector3 _inputCenter, _unprojectedCenter;
        private Transform _projectionOwner;
        private bool _custom;
        private float _nextProjection;
        private int _revision, _lineRevision = -1;
        private LineRenderer _line;
        private Color _color;
        private float _width;
        public bool HasShape { get; private set; }
        public int InputPointCount => _inputs.Count;
        public IReadOnlyList<Vector3> Points => _points;
        public Vector3 Center { get; private set; }
        public long GeometryBuildCount { get; private set; }
        public long GroundProjectionCount { get; private set; }
        public long LineWriteCount { get; private set; }

        public void Refresh(IReadOnlyList<Vector3> inputs, Vector3 center, bool custom,
            Transform projectionOwner, Settings settings, bool force)
        {
            float now = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            bool rebuild = force || !HasShape || custom != _custom || !_inputCenter.Equals(center) ||
                _projectionOwner != projectionOwner || !_settings.Equals(settings) || !SamePoints(inputs, _inputs);
            if (rebuild)
            {
                using var geometryScope = GeometryMarker.Auto();
                _inputs.Clear();
                for (int i = 0; i < inputs.Count; i++) _inputs.Add(inputs[i]);
                _inputCenter = center; _custom = custom; _settings = settings; _projectionOwner = projectionOwner;
                if (custom)
                {
                    _unprojected.Clear(); _unprojected.AddRange(_inputs); _unprojectedCenter = center;
                }
                else GameplayTargetShapeUtility.BuildSmoothRange(_inputs, center, settings.Padding, settings.Radius,
                    settings.CircleSegments, settings.SmoothSegments, _unprojected, out _unprojectedCenter, _hull);
                GeometryBuildCount++;
            }
            if (!rebuild && now < _nextProjection) return;
            using var projectionScope = ProjectionMarker.Auto();
            bool changed = !HasShape || _points.Count != _unprojected.Count;
            Vector3 projectedCenter = Project(_unprojectedCenter);
            changed |= !Center.Equals(projectedCenter);
            Center = projectedCenter;
            for (int i = 0; i < _unprojected.Count; i++)
            {
                Vector3 point = Project(_unprojected[i]);
                if (i >= _points.Count) _points.Add(point);
                else { changed |= !_points[i].Equals(point); _points[i] = point; }
            }
            if (_points.Count > _unprojected.Count) _points.RemoveRange(_unprojected.Count, _points.Count - _unprojected.Count);
            if (changed) _revision++;
            HasShape = true;
            GroundProjectionCount++;
            // Stagger owners without requiring a global scheduler. The maximum delay remains 0.5 game seconds.
            float phase = projectionOwner != null ? (projectionOwner.GetInstanceID() & 255) / 255f : 1f;
            _nextProjection = now + 0.25f + 0.25f * phase;
        }

        public void ApplyLine(LineRenderer line, Color color, float width, bool force)
        {
            if (line == null) return;
            width = Mathf.Max(0.01f, width);
            if (!force && _line == line && _lineRevision == _revision && _color.Equals(color) && _width == width &&
                line.positionCount == _points.Count && line.loop && line.useWorldSpace) return;
            using var lineScope = LineMarker.Auto();
            line.useWorldSpace = true; line.loop = true;
            line.startColor = color; line.endColor = color; line.widthMultiplier = width;
            line.positionCount = _points.Count;
            for (int i = 0; i < _points.Count; i++) line.SetPosition(i, _points[i]);
            _line = line; _lineRevision = _revision; _color = color; _width = width;
            LineWriteCount++;
        }

        private Vector3 Project(Vector3 point) => GameplayTargetShapeUtility.ProjectPointToGround(point, _projectionOwner,
            _settings.ProbeHeight, _settings.ProbeDistance, _settings.MinNormalY) + Vector3.up * _settings.HeightOffset;

        private static bool SamePoints(IReadOnlyList<Vector3> first, List<Vector3> second)
        {
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++) if (!first[i].Equals(second[i])) return false;
            return true;
        }
    }
}
