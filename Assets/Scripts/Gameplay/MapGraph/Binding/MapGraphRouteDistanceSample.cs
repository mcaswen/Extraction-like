using Gameplay.Agent.Data;
using UnityEngine;

namespace Gameplay.MapGraph.Binding
{
    public enum MapGraphDistanceSource { Unavailable, NativePath, QueriedPath, CachedQuery }
    /// <summary>一次只读距离事实；不持有原生 Path，不参与路线完成。</summary>
    public readonly struct MapGraphRouteDistanceSample
    {
        public AgentDirectiveRouteContext Context { get; }
        public Vector3 Anchor { get; }
        public bool IsValid { get; }
        public float RemainingDistance { get; }
        public float ArrivalTolerance { get; }
        public long PathVersion { get; }
        public MapGraphDistanceSource Source { get; }
        public string Failure { get; }
        public double SampleTime { get; }
        public MapGraphRouteDistanceSample(AgentDirectiveRouteContext context,Vector3 anchor,bool valid,float distance,
            float tolerance,long pathVersion,MapGraphDistanceSource source,string failure,double sampleTime)
        { Context=context; Anchor=anchor; IsValid=valid; RemainingDistance=distance; ArrivalTolerance=tolerance;
            PathVersion=pathVersion; Source=source; Failure=failure??string.Empty; SampleTime=sampleTime; }
    }
}
