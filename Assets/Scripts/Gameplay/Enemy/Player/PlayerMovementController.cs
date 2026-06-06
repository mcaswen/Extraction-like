using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
/// <summary>
/// 玩家移动控制器。
/// </summary>
public class PlayerMovementController : MonoBehaviour
{
    /// <summary>
    /// 玩家基础移动速度。
    /// </summary>
    public float MoveSpeed = 6f;

    /// <summary>
    /// 外部拉拽速度衰减系数。
    /// </summary>
    public float ExternalPullDamping = 14f;

    /// <summary>
    /// 外部击退速度衰减系数。
    /// </summary>
    public float ExternalImpulseDamping = 10f;

    /// <summary>
    /// 定身状态染色颜色。
    /// </summary>
    public Color ImmobilizeTintColor = new Color(0.42f, 0.72f, 1f, 1f);

    /// <summary>
    /// 定身状态染色强度。
    /// </summary>
    public float ImmobilizeTintStrength = 0.5f;

    private Rigidbody _playerRigidbody;
    private NavMeshAgent _navMeshAgent;
    private Camera _mainCamera;
    private Vector3 _externalPullVelocity;
    private Vector3 _externalImpulseVelocity;
    private float _immobilizeDurationRemaining;
    private float _speedBoostDurationRemaining;
    private float _speedBoostMultiplier = 1f;
    private float _speedDebuffDurationRemaining;
    private float _speedDebuffMultiplier = 1f;
    private float _nextFootstepStimulusTime;
    private Renderer[] _cachedRenderers;
    private Color[] _originalColors;
    private bool _originalUseGravity;
    private bool _originalIsKinematic;
    private bool _hasOriginalRigidbodySettings;
    private bool _isNavMeshDrivingRigidbody;

    public bool IsNavMeshDrivingRigidbody => _isNavMeshDrivingRigidbody;

    private void Start()
    {
        _playerRigidbody = GetComponent<Rigidbody>();
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _mainCamera = Camera.main;
        CacheRigidbodySettings();
        CacheRendererColors();
    }

    private void OnDisable()
    {
        SetNavMeshDrivingRigidbody(false);
    }

    private void FixedUpdate()
    {
        if (RaidFlowController.Instance != null && RaidFlowController.Instance.IsInputLocked)
        {
            return;
        }

        TickImmobilizeVisual();
        if (IsNavMeshAgentControllingMovement())
        {
            SetNavMeshDrivingRigidbody(true);
            TickMovementStateTimers();
            return;
        }

        SetNavMeshDrivingRigidbody(false);
        Move();
        Aim();
    }

    private void Move()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        if (_immobilizeDurationRemaining > 0f)
        {
            horizontal = 0f;
            vertical = 0f;
        }
        float effectiveMoveSpeed = MoveSpeed *
            (_speedBoostDurationRemaining > 0f ? _speedBoostMultiplier : 1f) *
            (_speedDebuffDurationRemaining > 0f ? _speedDebuffMultiplier : 1f);
        Vector3 movement = new Vector3(horizontal, 0f, vertical).normalized * effectiveMoveSpeed;
        Vector3 finalVelocity = movement + _externalPullVelocity + _externalImpulseVelocity;
        _playerRigidbody.MovePosition(_playerRigidbody.position + finalVelocity * Time.fixedDeltaTime);
        if (movement.sqrMagnitude > 0.01f && Time.time >= _nextFootstepStimulusTime)
        {
            // 玩家移动会周期性产生脚步刺激，供敌人巡逻感知系统调查。
            EnemySuspicionStimulusBus.ReportFootstep(transform.position, transform);
            _nextFootstepStimulusTime = Time.time + 0.65f;
        }
        TickMovementStateTimers();
    }

    private void TickMovementStateTimers()
    {
        _externalPullVelocity = Vector3.Lerp(_externalPullVelocity, Vector3.zero, ExternalPullDamping * Time.fixedDeltaTime);
        _externalImpulseVelocity = Vector3.Lerp(_externalImpulseVelocity, Vector3.zero, ExternalImpulseDamping * Time.fixedDeltaTime);
        _immobilizeDurationRemaining = Mathf.Max(0f, _immobilizeDurationRemaining - Time.fixedDeltaTime);
        _speedBoostDurationRemaining = Mathf.Max(0f, _speedBoostDurationRemaining - Time.unscaledDeltaTime);
        if (_speedBoostDurationRemaining <= 0f)
        {
            _speedBoostMultiplier = 1f;
        }

        _speedDebuffDurationRemaining = Mathf.Max(0f, _speedDebuffDurationRemaining - Time.unscaledDeltaTime);
        if (_speedDebuffDurationRemaining <= 0f)
        {
            _speedDebuffMultiplier = 1f;
        }
    }

    private bool IsNavMeshAgentControllingMovement()
    {
        return _navMeshAgent != null &&
               _navMeshAgent.enabled &&
               _navMeshAgent.isOnNavMesh &&
               (_navMeshAgent.pathPending || _navMeshAgent.hasPath || !_navMeshAgent.isStopped);
    }

    private void CacheRigidbodySettings()
    {
        if (_playerRigidbody == null || _hasOriginalRigidbodySettings)
            return;

        _originalUseGravity = _playerRigidbody.useGravity;
        _originalIsKinematic = _playerRigidbody.isKinematic;
        _hasOriginalRigidbodySettings = true;
    }

    private void SetNavMeshDrivingRigidbody(bool isNavMeshDriving)
    {
        if (_playerRigidbody == null || _isNavMeshDrivingRigidbody == isNavMeshDriving)
            return;

        _isNavMeshDrivingRigidbody = isNavMeshDriving;
        if (isNavMeshDriving)
        {
            CacheRigidbodySettings();
            _playerRigidbody.useGravity = false;
            _playerRigidbody.isKinematic = true;
            return;
        }

        if (!_hasOriginalRigidbodySettings)
            return;

        _playerRigidbody.useGravity = _originalUseGravity;
        _playerRigidbody.isKinematic = _originalIsKinematic;
    }

    private void Aim()
    {
        if (_mainCamera == null)
        {
            return;
        }

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        if (!groundPlane.Raycast(ray, out float rayDistance))
        {
            return;
        }

        Vector3 point = ray.GetPoint(rayDistance);
        Vector3 lookPosition = new Vector3(point.x, transform.position.y, point.z);
        transform.LookAt(lookPosition);
    }

    /// <summary>
    /// 施加一股朝目标点的轻微拉拽力。
    /// </summary>
    /// <param name="targetPosition">拉拽目标点。</param>
    /// <param name="pullStrength">拉拽强度。</param>
    public void ApplyExternalPull(Vector3 targetPosition, float pullStrength)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _externalPullVelocity += direction.normalized * pullStrength;
    }

    /// <summary>
    /// 施加一个瞬时击退/推力。
    /// </summary>
    /// <param name="direction">推力方向。</param>
    /// <param name="strength">推力强度。</param>
    public void ApplyExternalImpulse(Vector3 direction, float strength)
    {
        Vector3 planarDirection = direction;
        planarDirection.y = 0f;
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _externalImpulseVelocity += planarDirection.normalized * strength;
    }

    /// <summary>
    /// 施加短时间定身。
    /// </summary>
    /// <param name="duration">定身持续时间。</param>
    public void ApplyImmobilize(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        _immobilizeDurationRemaining = Mathf.Max(_immobilizeDurationRemaining, duration);
        UpdateImmobilizeVisual();
    }

    /// <summary>
    /// 玩家当前是否处于定身状态。
    /// </summary>
    /// <returns>定身仍在持续时返回 true。</returns>
    public bool IsImmobilized()
    {
        return _immobilizeDurationRemaining > 0f;
    }

    /// <summary>
    /// 应用移动速度增益倍率。
    /// </summary>
    /// <param name="multiplier">速度倍率。</param>
    /// <param name="duration">持续时间。</param>
    public void ApplyMoveSpeedMultiplier(float multiplier, float duration)
    {
        if (duration <= 0f || multiplier <= 0f)
        {
            return;
        }

        _speedBoostDurationRemaining = Mathf.Max(_speedBoostDurationRemaining, duration);
        _speedBoostMultiplier = Mathf.Max(_speedBoostMultiplier, multiplier);
    }

    /// <summary>
    /// 应用移动速度减益倍率。
    /// </summary>
    /// <param name="multiplier">速度倍率。</param>
    /// <param name="duration">持续时间。</param>
    public void ApplyMoveSpeedDebuff(float multiplier, float duration)
    {
        if (duration <= 0f || multiplier <= 0f)
        {
            return;
        }

        _speedDebuffDurationRemaining = Mathf.Max(_speedDebuffDurationRemaining, duration);
        _speedDebuffMultiplier = Mathf.Min(_speedDebuffMultiplier, Mathf.Clamp(multiplier, 0.1f, 1f));
    }

    private void CacheRendererColors()
    {
        _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        _originalColors = new Color[_cachedRenderers.Length];

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            _originalColors[i] = rendererComponent != null && rendererComponent.material.HasProperty("_Color")
                ? rendererComponent.material.color
                : Color.white;
        }
    }

    private void TickImmobilizeVisual()
    {
        if (_immobilizeDurationRemaining <= 0f)
        {
            RestoreRendererColors();
            return;
        }

        UpdateImmobilizeVisual();
    }

    private void UpdateImmobilizeVisual()
    {
        if (_cachedRenderers == null || _originalColors == null)
        {
            return;
        }

        float pulse = 0.5f + Mathf.Sin(Time.time * 8f) * 0.5f;
        float tintStrength = ImmobilizeTintStrength * (0.55f + pulse * 0.45f);

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
            {
                continue;
            }

            rendererComponent.material.color = Color.Lerp(_originalColors[i], ImmobilizeTintColor, tintStrength);
        }
    }

    private void RestoreRendererColors()
    {
        if (_cachedRenderers == null || _originalColors == null)
        {
            return;
        }

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
            {
                continue;
            }

            rendererComponent.material.color = _originalColors[i];
        }
    }
}
