using UnityEngine;

namespace Gameplay.MapGraph.Runtime
{
    /// <summary>只根据有效距离更新线段比例，节点推进由执行层负责。</summary>
    public sealed class MapGraphAgentPositionProjection
    {
        private string _segmentId, _edgeId, _fromNode, _toNode;
        private bool _hasSegment, _hasBaseline, _wasValid;
        public float Progress01 { get; private set; }
        public float BaselineDistance { get; private set; }
        public float BaselineProgress { get; private set; }
        public float BaselineTolerance { get; private set; }
        public long PathVersion { get; private set; }
        public bool HasValidDistance { get; private set; }

        public float Update(string segmentId, string edgeId, string fromNode, string toNode,
            bool valid, float remainingDistance, float arrivalTolerance, long pathVersion)
        {
            bool changed = !_hasSegment || _segmentId != segmentId || _edgeId != edgeId || _fromNode != fromNode || _toNode != toNode;
            if (changed)
            {
                bool sameEdge = _hasSegment && !string.IsNullOrEmpty(edgeId) && _edgeId == edgeId;
                if (sameEdge && _fromNode == toNode && _toNode == fromNode) Progress01 = 1-Progress01;
                else if (!sameEdge || _fromNode != fromNode || _toNode != toNode) Progress01 = 0;
                _segmentId=segmentId; _edgeId=edgeId; _fromNode=fromNode; _toNode=toNode; _hasSegment=true; _hasBaseline=false;
            }
            HasValidDistance = valid && Finite(remainingDistance) && remainingDistance >= 0 && Finite(arrivalTolerance) && arrivalTolerance >= 0;
            if (!HasValidDistance) { _wasValid=false; return Progress01; }
            if (!_hasBaseline || !_wasValid || PathVersion != pathVersion)
            {
                BaselineDistance=remainingDistance; BaselineProgress=Progress01; BaselineTolerance=arrivalTolerance; PathVersion=pathVersion;
                _hasBaseline=true;
            }
            _wasValid=true;
            float denominator=BaselineDistance-BaselineTolerance;
            Progress01=denominator<=0.0001f ? (remainingDistance<=BaselineTolerance ? 1f : BaselineProgress) :
                Mathf.Clamp01(BaselineProgress+(1-BaselineProgress)*(BaselineDistance-remainingDistance)/denominator);
            return Progress01;
        }
        public static Vector2 PositionOnSegment(Vector2 fromPort, Vector2 toPort, float progress01) =>
            Vector2.Lerp(fromPort,toPort,Mathf.Clamp01(progress01));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
