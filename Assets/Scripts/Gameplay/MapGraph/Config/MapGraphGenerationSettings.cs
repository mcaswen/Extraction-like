using System;
using UnityEngine;

namespace Gameplay.MapGraph.Config
{
    /// <summary>自动生成的参数数据；计算、取消和预算消耗留在 Editor 生成模块。</summary>
    [Serializable]
    public sealed class MapGraphGenerationSettings
    {
        [SerializeField, Min(1)] private float _gridSpacing = 80;
        [SerializeField, Min(1)] private float _nodeDiameter = 28;
        [SerializeField, Min(0)] private float _zonePadding = 40;
        [SerializeField] private Vector2 _minimumZoneSize = new Vector2(240, 180);
        [SerializeField] private Vector2 _nameSafeSize = new Vector2(100, 28);
        [SerializeField, Min(0.01f)] private float _worldToMapScale = 1;
        [SerializeField, Min(1)] private int _candidateNeighbors = 4;
        [SerializeField, Min(1)] private int _maximumSearchStates = 12000;
        [SerializeField, Min(1)] private int _maximumLayoutIterations = 128;
        [SerializeField, Min(1)] private int _queriesPerEditorTick = 12;
        [SerializeField, Range(0, 1)] private float _extraConnectionRatio = 0.2f;
        [SerializeField, Min(0)] private float _positionWeight = 1;
        [SerializeField, Min(0)] private float _directionWeight = 2;
        [SerializeField, Min(0)] private float _distanceWeight = 0.5f;
        [SerializeField, Min(0)] private float _areaWeight = 0.1f;
        [SerializeField, Min(0)] private float _crossingWeight = 8;
        public float GridSpacing => Mathf.Max(1, _gridSpacing);
        public float NodeDiameter => Mathf.Max(1, _nodeDiameter);
        public float ZonePadding => Mathf.Max(0, _zonePadding);
        public Vector2 MinimumZoneSize => _minimumZoneSize;
        public Vector2 NameSafeSize => _nameSafeSize;
        public float WorldToMapScale => Mathf.Max(0.01f, _worldToMapScale);
        public int CandidateNeighbors => Mathf.Max(1, _candidateNeighbors);
        public int MaximumSearchStates => Mathf.Max(1, _maximumSearchStates);
        public int MaximumLayoutIterations => Mathf.Max(1, _maximumLayoutIterations);
        public int QueriesPerEditorTick => Mathf.Max(1, _queriesPerEditorTick);
        public float ExtraConnectionRatio => Mathf.Clamp01(_extraConnectionRatio);
        public float PositionWeight => Mathf.Max(0, _positionWeight);
        public float DirectionWeight => Mathf.Max(0, _directionWeight);
        public float DistanceWeight => Mathf.Max(0, _distanceWeight);
        public float AreaWeight => Mathf.Max(0, _areaWeight);
        public float CrossingWeight => Mathf.Max(0, _crossingWeight);
    }
}
