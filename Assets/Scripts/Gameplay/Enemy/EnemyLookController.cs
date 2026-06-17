using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 敌人看向意图的来源，用于决定转向速度和优先级语义。
/// </summary>
public enum EnemyLookIntentSource
{
    PathForward,
    MovingScan,
    WaitScan,
    Suspicion,
    Investigate,
    Combat
}

/// <summary>
/// 敌人视野朝向控制器。
/// 负责独立 VisionPivot 的巡逻扫描、怀疑看向、调查看向和战斗锁定。
/// </summary>
public sealed class EnemyLookController : MonoBehaviour
{
    [Header("Rig")]
    [SerializeField]
    private Transform _visionPivot;

    [SerializeField, Min(0f)]
    private float _pivotHeight = 1.15f;

    [Header("Movement Scan")]
    [SerializeField, Range(0f, 120f)]
    private float _movingScanArc = 52f;

    [SerializeField, Min(0.1f)]
    private float _movingScanIntervalMin = 0.75f;

    [SerializeField, Min(0.1f)]
    private float _movingScanIntervalMax = 1.7f;

    [SerializeField, Range(0f, 1f)]
    private float _movingForwardBias = 0.62f;

    [Header("Wait Scan")]
    [SerializeField, Range(0f, 180f)]
    private float _waitScanArc = 115f;

    [SerializeField, Min(0.1f)]
    private float _waitScanIntervalMin = 0.45f;

    [SerializeField, Min(0.1f)]
    private float _waitScanIntervalMax = 1.15f;

    [Header("Turning")]
    [SerializeField, Min(1f)]
    private float _patrolTurnSpeed = 185f;

    [SerializeField, Min(1f)]
    private float _alertTurnSpeed = 310f;

    [SerializeField, Min(1f)]
    private float _combatTurnSpeed = 540f;

    [SerializeField, Min(0.05f)]
    private float _externalIntentDuration = 0.8f;

    private NavMeshAgent _agent;
    private Vector3 _currentLookDirection;
    private Vector3 _scanDirection;
    private Vector3 _externalLookPosition;
    private EnemyLookIntentSource _externalIntentSource;
    private float _nextScanTime;
    private float _externalIntentExpireTime;
    private float _externalTurnSpeed;
    private bool _hasExternalIntent;

    /// <summary>
    /// 当前用于视野检测的节点，缺省时回退到敌人根节点。
    /// </summary>
    public Transform VisionTransform => _visionPivot != null ? _visionPivot : transform;

    /// <summary>
    /// 当前视野节点在水平面上的朝向。
    /// </summary>
    public Vector3 CurrentPlanarDirection => _currentLookDirection.sqrMagnitude > 0.0001f ? _currentLookDirection.normalized : transform.forward;

    /// <summary>
    /// 当前外部看向意图的目标位置。
    /// </summary>
    public Vector3 CurrentExternalLookPosition => _externalLookPosition;

    /// <summary>
    /// 是否存在尚未过期的外部看向意图。
    /// </summary>
    public bool HasExternalIntent => _hasExternalIntent && Time.time <= _externalIntentExpireTime;

    private void Awake()
    {
        EnsureVisionPivot();
        _agent = GetComponent<NavMeshAgent>();
        _currentLookDirection = GetPlanarDirection(transform.forward, Vector3.forward);
        _scanDirection = _currentLookDirection;
        _nextScanTime = Time.time;
    }

    private void LateUpdate()
    {
        EnsureVisionPivot();
    }

    /// <summary>
    /// 驱动敌人在普通巡逻状态下的视野扫描。
    /// </summary>
    /// <param name="isWaiting">敌人是否正在巡逻点等待。</param>
    public void TickPatrolLook(bool isWaiting)
    {
        TickPatrolLook(isWaiting, null, -1f);
    }

    /// <summary>
    /// 驱动敌人巡逻看向，并允许路点覆盖等待注视目标和扫描角度。
    /// </summary>
    /// <param name="isWaiting">敌人是否正在巡逻点等待。</param>
    /// <param name="waitLookTarget">等待时优先注视的目标。</param>
    /// <param name="waitScanArcOverride">等待扫描角度覆盖值。</param>
    public void TickPatrolLook(bool isWaiting, Transform waitLookTarget, float waitScanArcOverride)
    {
        EnsureVisionPivot();

        if (TryTickExternalIntent())
        {
            return;
        }

        if (isWaiting && waitLookTarget != null)
        {
            TurnVisionTowardsPosition(waitLookTarget.position, _patrolTurnSpeed);
            return;
        }

        // 移动时以路径方向为主、随机扫描为辅；等待时加大扫描权重。
        Vector3 baseDirection = ResolvePathDirection();
        float scanArc = isWaiting
            ? (waitScanArcOverride >= 0f ? waitScanArcOverride : _waitScanArc)
            : _movingScanArc;
        float intervalMin = isWaiting ? _waitScanIntervalMin : _movingScanIntervalMin;
        float intervalMax = isWaiting ? _waitScanIntervalMax : _movingScanIntervalMax;

        if (Time.time >= _nextScanTime || _scanDirection.sqrMagnitude <= 0.0001f)
        {
            _scanDirection = ChooseScanDirection(baseDirection, scanArc, isWaiting);
            _nextScanTime = Time.time + Random.Range(intervalMin, Mathf.Max(intervalMin, intervalMax));
        }

        float weight = isWaiting ? 1f : 1f - _movingForwardBias;
        Vector3 desiredDirection = Vector3.Slerp(baseDirection, _scanDirection, weight);
        TurnVisionTowardsDirection(desiredDirection, _patrolTurnSpeed);
    }

    /// <summary>
    /// 设置一个临时外部看向意图，短时间覆盖巡逻扫描。
    /// </summary>
    /// <param name="worldPosition">需要看向的世界坐标。</param>
    /// <param name="source">看向意图来源。</param>
    /// <param name="duration">持续时间，小于等于 0 时使用默认值。</param>
    /// <param name="turnSpeed">转向速度，小于等于 0 时按来源自动解析。</param>
    public void LookAtPosition(
        Vector3 worldPosition,
        EnemyLookIntentSource source = EnemyLookIntentSource.Suspicion,
        float duration = -1f,
        float turnSpeed = -1f)
    {
        _externalLookPosition = worldPosition;
        _externalIntentSource = source;
        _externalIntentExpireTime = Time.time + (duration > 0f ? duration : _externalIntentDuration);
        _externalTurnSpeed = turnSpeed > 0f ? turnSpeed : ResolveTurnSpeed(source);
        _hasExternalIntent = true;
    }

    /// <summary>
    /// 短时间看向玩家，用于战斗状态或直接受击反应。
    /// </summary>
    /// <param name="playerTransform">玩家或当前战斗目标。</param>
    public void LookAtPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            return;
        }

        LookAtPosition(playerTransform.position, EnemyLookIntentSource.Combat, 0.25f, _combatTurnSpeed);
    }

    /// <summary>
    /// 清除当前外部看向意图，恢复巡逻扫描控制。
    /// </summary>
    public void ClearExternalIntent()
    {
        _hasExternalIntent = false;
    }

    /// <summary>
    /// 将敌人身体朝向逐步贴近当前视野朝向。
    /// </summary>
    /// <param name="turnSpeed">身体转向速度。</param>
    public void SnapBodyTowardsVision(float turnSpeed)
    {
        Vector3 direction = CurrentPlanarDirection;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 使用巡逻转向速度让身体逐步贴近当前视野朝向。
    /// </summary>
    public void SnapBodyTowardsVisionAtPatrolSpeed()
    {
        SnapBodyTowardsVision(_patrolTurnSpeed);
    }

    private bool TryTickExternalIntent()
    {
        if (!_hasExternalIntent)
        {
            return false;
        }

        if (Time.time > _externalIntentExpireTime)
        {
            _hasExternalIntent = false;
            return false;
        }

        TurnVisionTowardsPosition(_externalLookPosition, _externalTurnSpeed);
        return true;
    }

    private Vector3 ResolvePathDirection()
    {
        if (_agent == null)
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        if (_agent != null && _agent.enabled)
        {
            Vector3 steeringDelta = _agent.steeringTarget - transform.position;
            steeringDelta.y = 0f;
            if (steeringDelta.sqrMagnitude > 0.09f)
            {
                return steeringDelta.normalized;
            }

            Vector3 velocity = _agent.velocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude > 0.04f)
            {
                return velocity.normalized;
            }
        }

        return GetPlanarDirection(transform.forward, Vector3.forward);
    }

    private Vector3 ChooseScanDirection(Vector3 baseDirection, float scanArc, bool isWaiting)
    {
        float halfArc = Mathf.Max(0f, scanArc) * 0.5f;
        float yaw;
        if (isWaiting)
        {
            // 等待时从固定槽位中抽样，形成左右张望的节奏，而不是完全随机抖动。
            int slot = Random.Range(0, 5);
            switch (slot)
            {
                case 0:
                    yaw = -halfArc;
                    break;
                case 1:
                    yaw = -halfArc * 0.5f;
                    break;
                case 2:
                    yaw = 0f;
                    break;
                case 3:
                    yaw = halfArc * 0.5f;
                    break;
                default:
                    yaw = halfArc;
                    break;
            }
            yaw += Random.Range(-8f, 8f);
        }
        else
        {
            yaw = Random.Range(-halfArc, halfArc);
        }

        return Quaternion.Euler(0f, yaw, 0f) * baseDirection;
    }

    private void TurnVisionTowardsPosition(Vector3 worldPosition, float turnSpeed)
    {
        Vector3 direction = worldPosition - VisionTransform.position;
        direction.y = 0f;
        TurnVisionTowardsDirection(direction, turnSpeed);
    }

    private void TurnVisionTowardsDirection(Vector3 direction, float turnSpeed)
    {
        direction = GetPlanarDirection(direction, _currentLookDirection);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        VisionTransform.rotation = Quaternion.RotateTowards(
            VisionTransform.rotation,
            targetRotation,
            Mathf.Max(1f, turnSpeed) * Time.deltaTime);
        _currentLookDirection = GetPlanarDirection(VisionTransform.forward, direction);
    }

    private float ResolveTurnSpeed(EnemyLookIntentSource source)
    {
        switch (source)
        {
            case EnemyLookIntentSource.Combat:
                return _combatTurnSpeed;
            case EnemyLookIntentSource.Suspicion:
            case EnemyLookIntentSource.Investigate:
                return _alertTurnSpeed;
            default:
                return _patrolTurnSpeed;
        }
    }

    private void EnsureVisionPivot()
    {
        if (_visionPivot != null)
        {
            return;
        }

        Transform existingPivot = transform.Find("VisionPivot");
        if (existingPivot != null)
        {
            _visionPivot = existingPivot;
            return;
        }

        GameObject pivotObject = new GameObject("VisionPivot");
        pivotObject.transform.SetParent(transform, false);
        pivotObject.transform.localPosition = Vector3.up * _pivotHeight;
        pivotObject.transform.localRotation = Quaternion.identity;
        _visionPivot = pivotObject.transform;
    }

    private static Vector3 GetPlanarDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        fallback.y = 0f;
        if (fallback.sqrMagnitude > 0.0001f)
        {
            return fallback.normalized;
        }

        return Vector3.forward;
    }

    private void OnDrawGizmosSelected()
    {
        Transform pivot = _visionPivot != null ? _visionPivot : transform;
        Gizmos.color = new Color(0.22f, 0.84f, 1f, 1f);
        Gizmos.DrawLine(pivot.position, pivot.position + pivot.forward * 2.2f);

        if (HasExternalIntent)
        {
            Gizmos.color = new Color(1f, 0.72f, 0.08f, 1f);
            Gizmos.DrawLine(pivot.position, _externalLookPosition + Vector3.up * _pivotHeight);
            Gizmos.DrawWireSphere(_externalLookPosition, 0.35f);
        }
    }
}
