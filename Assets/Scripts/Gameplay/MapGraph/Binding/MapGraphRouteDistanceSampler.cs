using Gameplay.Agent.Data;
using Gameplay.Agent.Interfaces;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.MapGraph.Binding
{
    /// <summary>按真实步骤采样原群距离。可借用已验证的 native 路径，其余查询独立限频。</summary>
    public sealed class MapGraphRouteDistanceSampler
    {
        private readonly AgentNavigationSegmentQuery.Buffer _buffer=new AgentNavigationSegmentQuery.Buffer();
        private AgentDirectiveRouteContext _context;
        private Vector3 _anchor;
        private Vector3 _sampledAnchor;
        private AgentNavigationProfile _sampleProfile;
        private double _nextQuery;
        private long _pathVersion;
        private bool _hasQuery, _wasValid;
        private AgentNavigationSegmentResult _lastQuery;
        public long CalculationCount => _buffer.CalculationCount;
        public MapGraphRouteDistanceSample LastSample { get; private set; }

        public MapGraphRouteDistanceSample Sample(IAgentReadOnly agent, AgentRouteSnapshot route, double now, bool allowQuery=true)
        {
            var step=route.CurrentStep;
            if (!_context.Equals(step.Context) || (_anchor-step.Anchor).sqrMagnitude>0.0001f)
            { _context=step.Context; _anchor=step.Anchor; _hasQuery=false; _sampleProfile=null; _nextQuery=0; _wasValid=false; _pathVersion++; }
            float tolerance=Mathf.Max(AgentNavigationQuery.ArrivalTolerance,
                agent?.Blackboard?.GetValueOrDefault<float>(AgentBlackboardKeys.MoveStoppingDistance,2f)??2f);
            if (agent==null || !route.IsActive || step.Phase!=AgentClusterStepPhase.Travelling || !step.Context.IsValid)
                return Invalid("NoTravellingStep",tolerance,now);
            var nav=agent.NavMeshAgent;
            if (!AgentNavigationQuery.IsReady(nav)) return Invalid("NavigationNotReady",tolerance,now);
            if (nav.pathPending) return Invalid("PathPending",tolerance,now);
            if (_sampleProfile==null || _sampleProfile.AgentTypeId!=nav.agentTypeID || _sampleProfile.AreaMask!=nav.areaMask ||
                _sampleProfile.SampleRadius!=Mathf.Max(0.5f,nav.radius*2) || _sampleProfile.HeightTolerance!=Mathf.Max(0.5f,nav.height*0.5f))
            {
                _sampleProfile=AgentNavigationProfile.FromAgent(nav);
                if (!AgentNavigationSegmentQuery.TrySampleAnchor(_sampleProfile,_anchor,out _sampledAnchor,out string failure))
                { _sampleProfile=null; return Invalid(failure,tolerance,now); }
            }
            bool sameCommand=agent.Blackboard.TryGetValue(AgentBlackboardKeys.PendingDirectiveRequest,out AgentDirectiveRequest command) &&
                command.RouteContext.Equals(step.Context) && command.DirectiveType==AgentDirectiveType.MoveTo;
            if (sameCommand && nav.hasPath && (nav.isPathStale || nav.pathStatus!=NavMeshPathStatus.PathComplete))
                return Invalid(nav.isPathStale?"PathStale":"PathIncomplete",tolerance,now);
            if (sameCommand && nav.hasPath && !nav.isPathStale && nav.pathStatus==NavMeshPathStatus.PathComplete &&
                (nav.destination-_sampledAnchor).sqrMagnitude<=0.01f)
            {
                float distance=nav.remainingDistance;
                if (!Finite(distance) || distance<0) return Invalid("NativeDistanceInvalid",tolerance,now);
                return Valid(distance,tolerance,MapGraphDistanceSource.NativePath,now);
            }
            if (now>=_nextQuery && allowQuery)
            {
                _nextQuery=now+0.25; _lastQuery=AgentNavigationSegmentQuery.Calculate(nav,step.Anchor,_buffer); _hasQuery=true;
                if (!_lastQuery.IsComplete) return Invalid(_lastQuery.Failure,tolerance,now);
                _sampledAnchor=_lastQuery.Destination;
                return Valid(_lastQuery.Length,tolerance,MapGraphDistanceSource.QueriedPath,now);
            }
            if (!_hasQuery) return Invalid("QueryBudget",tolerance,now);
            return _lastQuery.IsComplete ? Valid(_lastQuery.Length,tolerance,MapGraphDistanceSource.CachedQuery,now) : Invalid(_lastQuery.Failure,tolerance,now);
        }
        private MapGraphRouteDistanceSample Invalid(string failure,float tolerance,double now)
        {
            _wasValid=false;
            return LastSample=new MapGraphRouteDistanceSample(_context,_anchor,false,float.NaN,tolerance,_pathVersion,MapGraphDistanceSource.Unavailable,failure,now);
        }
        private MapGraphRouteDistanceSample Valid(float distance,float tolerance,MapGraphDistanceSource source,double now)
        {
            if (!_wasValid) _pathVersion++;
            _wasValid=true;
            return LastSample=new MapGraphRouteDistanceSample(_context,_anchor,true,distance,tolerance,_pathVersion,source,string.Empty,now);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
