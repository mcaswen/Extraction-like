using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.Agent.Animation
{
    /// <summary>
    /// Agent 动画控制桥接器
    /// 负责把运行时移动速度和攻击事件写入 Animator 参数与战斗层
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
            if (!HasPlayableAnimator())
            {
                _previousPosition = transform.position;
                return;
            }

            float worldSpeed = ResolvePlanarSpeed();
            float blendTreeSpeed = ConvertWorldSpeedToBlendTreeThreshold(worldSpeed);
            bool isMoving = worldSpeed > _stationarySpeedThreshold;
            bool isAttacking = Time.time < _attackUntilTime;

            // 移动参数允许动画控制器缺省，避免不同 Animator 复用时硬依赖全部参数
            SetOptionalFloat(_speedParameterHash, blendTreeSpeed, _speedDampSeconds);
            SetOptionalFloat(_worldSpeedParameterHash, worldSpeed, _speedDampSeconds);
            SetOptionalBool(_isMovingParameterHash, isMoving);
            SetOptionalBool(_isAttackingParameterHash, isAttacking);
            UpdateCombatLayerWeight(isAttacking);
        }

        /// <summary>
        /// 通知动画层播放一次攻击表现
        /// </summary>
        /// <param name="suggestedDurationSeconds"></param>
        public void NotifyAttack(float suggestedDurationSeconds)
        {
            if (!HasPlayableAnimator())
                return;

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

            // NavMeshAgent 正在驱动时优先使用它的实时速度或期望速度
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
                // Rigidbody 或 Transform 被外部驱动时，使用帧间位移作为兜底速度
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

            if (!HasPlayableAnimator())
                return;

            // 缓存 Animator 参数类型，让写入前能检查参数是否存在且类型正确
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

            if (!HasPlayableAnimator())
                return;

            if (_combatLayerIndex < 0)
                return;

            // 战斗层默认隐藏，真正攻击时再淡入并切到攻击状态
            _animator.SetLayerWeight(_combatLayerIndex, 0f);
            TryResolveStateHash(
                _combatLayerIndex,
                _combatLayerName,
                _attackStateName,
                out _attackStateHash);
        }

        private int ResolveLayerIndex(string layerName)
        {
            if (!HasPlayableAnimator() || string.IsNullOrEmpty(layerName))
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

            // 攻击窗口内淡入战斗层，攻击结束后按配置淡出回移动层
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
            if (!HasPlayableAnimator() || layerIndex < 0 || string.IsNullOrEmpty(stateName))
                return false;

            // 先尝试短状态名，兼容 Animator 层内唯一状态
            int shortNameHash = Animator.StringToHash(stateName);
            if (_animator.HasState(layerIndex, shortNameHash))
            {
                stateHash = shortNameHash;
                return true;
            }

            if (!string.IsNullOrEmpty(layerName))
            {
                // 再尝试 Layer.State 全路径，兼容同名状态或 Unity 的完整路径哈希
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

        private bool HasPlayableAnimator()
        {
            return _animator != null && _animator.runtimeAnimatorController != null;
        }
    }
}
