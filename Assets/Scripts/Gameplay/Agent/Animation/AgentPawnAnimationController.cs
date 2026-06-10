using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Animation
{
    /// <summary>
    /// Bridges Agent runtime body facts into the character Animator.
    /// Movement is written into the controller's blend-tree threshold space.
    /// Attack is reported as an Animator trigger and blended through the configured combat layer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AgentPawnAnimationController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private Animator _animator;
        [SerializeField] private NavMeshAgent _navMeshAgent;

        [Header("Animator Parameters")]
        [SerializeField] private string _speedParameterName = "Speed";
        [SerializeField] private string _attackTriggerName = "Attack";
        [SerializeField] private string _worldSpeedParameterName = "MoveSpeed";
        [SerializeField] private string _isMovingParameterName = "IsMoving";
        [SerializeField] private string _isAttackingParameterName = "IsAttacking";

        [Header("Combat Layer")]
        [SerializeField] private string _combatLayerName = "Combat Layer";
        [SerializeField] private string _attackStateName = "Attack";
        [SerializeField, Min(0f)] private float _attackLayerFadeInSeconds = 0.04f;
        [SerializeField, Min(0f)] private float _attackLayerFadeOutSeconds = 0.12f;

        [Header("Blend Tree Thresholds")]
        [SerializeField] private float _idleBlendTreeThreshold = 0f;
        [SerializeField] private float _runBlendTreeThreshold = 1f;
        [SerializeField, Min(0f)] private float _worldSpeedAtRunThreshold = 0f;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float _stationarySpeedThreshold = 0.08f;
        [SerializeField, Min(0f)] private float _speedDampSeconds = 0.08f;
        [SerializeField, Min(0.05f)] private float _attackHoldSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float _maxAttackHoldSeconds = 1.2f;

        private readonly Dictionary<int, AnimatorControllerParameterType> _parameterTypes =
            new Dictionary<int, AnimatorControllerParameterType>();

        private int _speedParameterHash;
        private int _attackTriggerHash;
        private int _worldSpeedParameterHash;
        private int _isMovingParameterHash;
        private int _isAttackingParameterHash;
        private int _combatLayerIndex = -1;
        private int _attackStateHash;
        private float _combatLayerWeight;
        private float _attackUntilTime;
        private Vector3 _previousPosition;

        private void Awake()
        {
            CacheComponents();
            RefreshParameterHashes();
            RefreshParameterCache();
            RefreshLayerBindings();
            _previousPosition = transform.position;
        }

        private void Reset()
        {
            CacheComponents();
        }

        private void OnValidate()
        {
            _stationarySpeedThreshold = Mathf.Max(0f, _stationarySpeedThreshold);
            _speedDampSeconds = Mathf.Max(0f, _speedDampSeconds);
            _attackLayerFadeInSeconds = Mathf.Max(0f, _attackLayerFadeInSeconds);
            _attackLayerFadeOutSeconds = Mathf.Max(0f, _attackLayerFadeOutSeconds);
            _attackHoldSeconds = Mathf.Max(0.05f, _attackHoldSeconds);
            _maxAttackHoldSeconds = Mathf.Max(_attackHoldSeconds, _maxAttackHoldSeconds);
            CacheComponents();
            RefreshParameterHashes();
            RefreshLayerBindings();
        }

        private void OnEnable()
        {
            RefreshParameterHashes();
            RefreshParameterCache();
            RefreshLayerBindings();
            _previousPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (_animator == null)
                return;

            float worldSpeed = ResolvePlanarSpeed();
            float blendTreeSpeed = ConvertWorldSpeedToBlendTreeThreshold(worldSpeed);
            bool isMoving = worldSpeed > _stationarySpeedThreshold;
            bool isAttacking = Time.time < _attackUntilTime;

            SetOptionalFloat(_speedParameterHash, blendTreeSpeed, _speedDampSeconds);
            SetOptionalFloat(_worldSpeedParameterHash, worldSpeed, _speedDampSeconds);
            SetOptionalBool(_isMovingParameterHash, isMoving);
            SetOptionalBool(_isAttackingParameterHash, isAttacking);
            UpdateCombatLayerWeight(isAttacking);
        }

        public void NotifyAttack(float suggestedDurationSeconds)
        {
            float duration = Mathf.Clamp(
                suggestedDurationSeconds > 0f ? suggestedDurationSeconds : _attackHoldSeconds,
                _attackHoldSeconds,
                _maxAttackHoldSeconds);

            _attackUntilTime = Mathf.Max(_attackUntilTime, Time.time + duration);
            SetOptionalTrigger(_attackTriggerHash);
            PlayAttackOnCombatLayer();
            UpdateCombatLayerWeight(true);
        }

        private float ResolvePlanarSpeed()
        {
            Vector3 velocity = Vector3.zero;

            if (_navMeshAgent != null && _navMeshAgent.enabled)
            {
                velocity = _navMeshAgent.velocity;
                if (velocity.sqrMagnitude <= 0.0001f && _navMeshAgent.hasPath)
                    velocity = _navMeshAgent.desiredVelocity;
            }

            velocity.y = 0f;
            float speed = velocity.magnitude;

            if (speed <= _stationarySpeedThreshold && Time.deltaTime > 0f)
            {
                Vector3 frameDelta = transform.position - _previousPosition;
                frameDelta.y = 0f;
                speed = Mathf.Max(speed, frameDelta.magnitude / Time.deltaTime);
            }

            _previousPosition = transform.position;
            return speed;
        }

        private float ConvertWorldSpeedToBlendTreeThreshold(float worldSpeed)
        {
            if (worldSpeed <= _stationarySpeedThreshold)
                return _idleBlendTreeThreshold;

            float runWorldSpeed = ResolveRunWorldSpeed();
            float normalizedSpeed = runWorldSpeed <= _stationarySpeedThreshold
                ? 1f
                : Mathf.InverseLerp(_stationarySpeedThreshold, runWorldSpeed, worldSpeed);

            return Mathf.Lerp(_idleBlendTreeThreshold, _runBlendTreeThreshold, normalizedSpeed);
        }

        private float ResolveRunWorldSpeed()
        {
            if (_worldSpeedAtRunThreshold > 0f)
                return _worldSpeedAtRunThreshold;

            if (_navMeshAgent != null && _navMeshAgent.enabled && _navMeshAgent.speed > 0f)
                return _navMeshAgent.speed;

            return Mathf.Max(1f, _stationarySpeedThreshold);
        }

        private void CacheComponents()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            if (_navMeshAgent == null)
                _navMeshAgent = GetComponent<NavMeshAgent>();
        }

        private void RefreshParameterHashes()
        {
            _speedParameterHash = ToParameterHash(_speedParameterName);
            _attackTriggerHash = ToParameterHash(_attackTriggerName);
            _worldSpeedParameterHash = ToParameterHash(_worldSpeedParameterName);
            _isMovingParameterHash = ToParameterHash(_isMovingParameterName);
            _isAttackingParameterHash = ToParameterHash(_isAttackingParameterName);
        }

        private void RefreshParameterCache()
        {
            _parameterTypes.Clear();

            if (_animator == null)
                return;

            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                _parameterTypes[parameter.nameHash] = parameter.type;
            }
        }

        private void RefreshLayerBindings()
        {
            _combatLayerIndex = ResolveLayerIndex(_combatLayerName);
            _attackStateHash = 0;
            _combatLayerWeight = 0f;

            if (_combatLayerIndex < 0)
                return;

            _animator.SetLayerWeight(_combatLayerIndex, 0f);
            TryResolveStateHash(
                _combatLayerIndex,
                _combatLayerName,
                _attackStateName,
                out _attackStateHash);
        }

        private int ResolveLayerIndex(string layerName)
        {
            if (_animator == null || string.IsNullOrEmpty(layerName))
                return -1;

            for (int i = 0; i < _animator.layerCount; i++)
            {
                if (_animator.GetLayerName(i) == layerName)
                    return i;
            }

            return -1;
        }

        private void PlayAttackOnCombatLayer()
        {
            if (_combatLayerIndex < 0 || _attackStateHash == 0)
                return;

            _animator.CrossFade(_attackStateHash, _attackLayerFadeInSeconds, _combatLayerIndex, 0f);
        }

        private void UpdateCombatLayerWeight(bool isAttacking)
        {
            if (_combatLayerIndex < 0)
                return;

            float targetWeight = isAttacking ? 1f : 0f;
            float fadeSeconds = isAttacking ? _attackLayerFadeInSeconds : _attackLayerFadeOutSeconds;

            _combatLayerWeight = fadeSeconds <= 0f
                ? targetWeight
                : Mathf.MoveTowards(
                    _combatLayerWeight,
                    targetWeight,
                    Time.deltaTime / fadeSeconds);

            _animator.SetLayerWeight(_combatLayerIndex, _combatLayerWeight);
        }

        private void SetOptionalFloat(int parameterHash, float value, float dampSeconds)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Float))
            {
                if (dampSeconds > 0f)
                    _animator.SetFloat(parameterHash, value, dampSeconds, Time.deltaTime);
                else
                    _animator.SetFloat(parameterHash, value);
            }
        }

        private void SetOptionalBool(int parameterHash, bool value)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Bool))
                _animator.SetBool(parameterHash, value);
        }

        private void SetOptionalTrigger(int parameterHash)
        {
            if (HasParameter(parameterHash, AnimatorControllerParameterType.Trigger))
                _animator.SetTrigger(parameterHash);
        }

        private bool HasParameter(int parameterHash, AnimatorControllerParameterType expectedType)
        {
            return _parameterTypes.TryGetValue(parameterHash, out AnimatorControllerParameterType actualType) &&
                   actualType == expectedType;
        }

        private bool TryResolveStateHash(
            int layerIndex,
            string layerName,
            string stateName,
            out int stateHash)
        {
            stateHash = 0;
            if (_animator == null || layerIndex < 0 || string.IsNullOrEmpty(stateName))
                return false;

            int shortNameHash = Animator.StringToHash(stateName);
            if (_animator.HasState(layerIndex, shortNameHash))
            {
                stateHash = shortNameHash;
                return true;
            }

            if (!string.IsNullOrEmpty(layerName))
            {
                int fullPathHash = Animator.StringToHash($"{layerName}.{stateName}");
                if (_animator.HasState(layerIndex, fullPathHash))
                {
                    stateHash = fullPathHash;
                    return true;
                }
            }

            return false;
        }

        private static int ToParameterHash(string parameterName)
        {
            return string.IsNullOrEmpty(parameterName) ? 0 : Animator.StringToHash(parameterName);
        }
    }
}
