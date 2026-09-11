using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Navigation
{
    /// <summary>One movement monitor per pawn. It never chooses targets or writes task/UI state.</summary>
    public sealed class AgentNavigationMotor
    {
        private readonly NavMeshAgent _agent;
        private readonly float _readyTimeout;
        private readonly float _progressTimeout;
        private string _commandId;
        private float _notReadySince = -1f;
        private float _progressTime;
        private Vector3 _progressPosition;
        private float _lastPathTime = -999f;
        private readonly AgentNavigationQuery.Buffer _queryBuffer = new AgentNavigationQuery.Buffer();
        private AgentNavigationResult _lastResult;
        private bool _hasQuery;
        private Vector3 _target, _queryPosition;
        private float _distance, _radius, _height;
        private int _areaMask;
        public long PathCalculationCount => _queryBuffer.CalculationCount;
        public AgentNavigationMotor(NavMeshAgent agent, float readyTimeout, float progressTimeout)
        { _agent = agent; _readyTimeout = readyTimeout; _progressTimeout = progressTimeout; }

        public void Reset(string commandId)
        {
            Stop(); _commandId = commandId; _notReadySince = -1f; _progressTime = Time.time;
            _progressPosition = _agent != null ? _agent.transform.position : default; _lastPathTime = -999f; _hasQuery = false;
        }

        public AgentNavigationResult Move(string commandId, Vector3 target, float distance, float speed)
        {
            if (_commandId != commandId) Reset(commandId);
            if (!AgentNavigationQuery.IsReady(_agent))
            {
                _hasQuery = false;
                if (_notReadySince < 0f) _notReadySince = Time.time;
                global::RuntimeNavMeshSurfaceBuilder.Instance?.RequestRebuild();
                return Time.time - _notReadySince >= _readyTimeout
                    ? new AgentNavigationResult(AgentNavigationStatus.Unreachable) : new AgentNavigationResult(AgentNavigationStatus.NotReady);
            }
            _notReadySince = -1f;
            bool moving = _hasQuery && _lastResult.Status == AgentNavigationStatus.Moving;
            bool query = !_hasQuery || Time.time - _lastPathTime >= 0.1f ||
                (_target - target).sqrMagnitude > 0.0001f || _distance != distance ||
                _areaMask != _agent.areaMask || _radius != _agent.radius || _height != _agent.height ||
                (moving && (!_agent.hasPath || _agent.isPathStale || _agent.pathStatus != NavMeshPathStatus.PathComplete ||
                    (!_agent.pathPending && _agent.remainingDistance <= Mathf.Max(AgentNavigationQuery.ArrivalTolerance, distance) + 0.05f))) ||
                (!moving && (_queryPosition - _agent.nextPosition).sqrMagnitude > 0.0004f);
            if (query)
            {
                _lastResult = AgentNavigationQuery.Check(_agent, target, distance, _queryBuffer);
                _target = target; _distance = distance; _queryPosition = _agent.nextPosition;
                _areaMask = _agent.areaMask; _radius = _agent.radius; _height = _agent.height;
                _lastPathTime = Time.time; _hasQuery = true;
            }
            AgentNavigationResult result = _lastResult;
            if (result.Status != AgentNavigationStatus.Moving) { Stop(); _progressTime = Time.time; return result; }
            if (Time.timeScale <= 0f) { _progressTime = Time.time; return result; }
            if (Vector3.Distance(_progressPosition, _agent.nextPosition) >= 0.05f)
            { _progressPosition = _agent.nextPosition; _progressTime = Time.time; }
            if (Time.time - _progressTime >= _progressTimeout)
            { Stop(); return new AgentNavigationResult(AgentNavigationStatus.Stalled); }
            _agent.speed = Mathf.Max(0f, speed);
            _agent.acceleration = Mathf.Max(_agent.acceleration, speed * 2f);
            _agent.stoppingDistance = Mathf.Max(AgentNavigationQuery.ArrivalTolerance, distance);
            _agent.isStopped = false;
            if (query)
            {
                if (!_agent.SetPath(result.Path)) { _hasQuery = false; return new AgentNavigationResult(AgentNavigationStatus.Unreachable); }
            }
            return result;
        }

        public void Stop()
        {
            if (!AgentNavigationQuery.IsReady(_agent)) return;
            _agent.isStopped = true; _agent.ResetPath(); _agent.velocity = Vector3.zero;
            _progressPosition = _agent.nextPosition; _progressTime = Time.time;
        }
    }
}
