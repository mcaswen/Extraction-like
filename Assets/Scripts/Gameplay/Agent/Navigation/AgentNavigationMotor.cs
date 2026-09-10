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
        private Vector3 _destination;
        public AgentNavigationMotor(NavMeshAgent agent, float readyTimeout, float progressTimeout)
        { _agent = agent; _readyTimeout = readyTimeout; _progressTimeout = progressTimeout; }

        public void Reset(string commandId)
        {
            Stop(); _commandId = commandId; _notReadySince = -1f; _progressTime = Time.time;
            _progressPosition = _agent != null ? _agent.transform.position : default; _lastPathTime = -999f;
        }

        public AgentNavigationResult Move(string commandId, Vector3 target, float distance, float speed)
        {
            if (_commandId != commandId) Reset(commandId);
            AgentNavigationResult result = AgentNavigationQuery.Check(_agent, target, distance);
            if (result.Status == AgentNavigationStatus.NotReady)
            {
                if (_notReadySince < 0f) _notReadySince = Time.time;
                global::RuntimeNavMeshSurfaceBuilder.Instance?.RequestRebuild();
                return Time.time - _notReadySince >= _readyTimeout
                    ? new AgentNavigationResult(AgentNavigationStatus.Unreachable) : result;
            }
            _notReadySince = -1f;
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
            if (!_agent.hasPath || Time.time - _lastPathTime >= 0.1f || Vector3.Distance(_destination, result.Destination) > 0.1f)
            {
                if (!_agent.SetPath(result.Path)) return new AgentNavigationResult(AgentNavigationStatus.Unreachable);
                _destination = result.Destination; _lastPathTime = Time.time;
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
