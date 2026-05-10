using Gameplay.SkillEffect;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 现代搁浅者白模行为。
/// 使用触手吸附玩家，并施加腐蚀性黏液的持续伤害。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ModernStranderBehaviorController : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        Attack
    }

    public EnemyState CurrentState;

    [Header("References")]
    public Transform PlayerTransform;
    public Transform TentacleOrigin;
    public LineRenderer TentacleRenderer;

    [Header("Patrol")]
    public float PatrolRadius = 8f;
    public float PatrolWaitTime = 1.5f;

    [Header("Detection")]
    public float DetectionRange = 12f;
    public float LoseRange = 16f;

    [Header("Tentacle Attack")]
    public float AttackRange = 3.2f;
    public float AttackInterval = 2f;
    public float TentacleLatchDuration = 1.1f;
    public float TentacleHitboxWidth = 0.55f;
    public float TentacleHitboxHeight = 0.55f;
    public float LatchPullStrength = 3.4f;
    public float CorrosionDamagePerSecond = 5f;
    public float CorrosionDuration = 2.5f;
    public float CorrosionTickInterval = 0.25f;
    public float InitialContactDamage = 6f;

    [Header("Corrosive Slime")]
    public GameObject CorrosivePuddlePrefab;
    public float PuddleLifetime = 5f;
    public float PuddleRadius = 1.1f;
    public float PuddleDamagePerSecond = 6f;
    public float PuddleCorrosionDuration = 1.8f;
    public float PuddleTickInterval = 0.25f;

    private NavMeshAgent _navMeshAgent;
    private PlayerHealthController _playerHealthController;
    private PlayerMovementController _playerMovementController;
    private Vector3 _startingPosition;
    private float _waitTimer;
    private float _attackTimer;
    private float _latchTimer;
    private float _tentacleTotalDamage;
    private bool _isTentacleStriking;
    private bool _isTentacleLatched;
    private bool _hasAppliedInitialLatchDamage;
    private bool _hasAddedTentacleCorrosionDamage;
    private ModernStranderTentacleHitbox _tentacleHitbox;

    private void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _startingPosition = transform.position;
        CurrentState = EnemyState.Patrol;
        EnsureAgentReady();
        EnsurePlayerReferences();

        EnsureTentacleRenderer();
        EnsureTentacleHitbox();
        GetNewPatrolPoint();
    }

    private void Update()
    {
        if (!EnsurePlayerReferences())
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
        switch (CurrentState)
        {
            case EnemyState.Patrol:
                PatrolBehavior(distanceToPlayer);
                break;
            case EnemyState.Chase:
                ChaseBehavior(distanceToPlayer);
                break;
            case EnemyState.Attack:
                AttackBehavior(distanceToPlayer);
                break;
        }

        UpdateTentacleVisual();
        UpdateTentacleHitbox();
    }

    private void OnDisable()
    {
        bool hadActiveTentacle = _isTentacleStriking || _isTentacleLatched;
        float totalDamage = _tentacleTotalDamage;

        // Space-hourglass magic seal can disable this behaviour mid-attack.
        // Ensure any active tentacle latch/hitbox stops immediately.
        _isTentacleStriking = false;
        _isTentacleLatched = false;
        _latchTimer = 0f;
        _tentacleTotalDamage = 0f;
        _hasAppliedInitialLatchDamage = false;
        _hasAddedTentacleCorrosionDamage = false;
        SetTentacleHitboxEnabled(false);

        if (TentacleRenderer != null)
        {
            TentacleRenderer.enabled = false;
        }

        if (hadActiveTentacle)
        {
            EnemySkillDamageLogger.LogSkillDamage(this, "Corrosive Tentacle", totalDamage);
        }
    }

    private void PatrolBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer <= DetectionRange)
        {
            CurrentState = EnemyState.Chase;
            return;
        }

        if (HasReachedCurrentDestination())
        {
            _waitTimer += Time.deltaTime;
            if (_waitTimer >= PatrolWaitTime)
            {
                GetNewPatrolPoint();
                _waitTimer = 0f;
            }
        }
    }

    private void ChaseBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange)
        {
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer <= AttackRange)
        {
            CurrentState = EnemyState.Attack;
            _attackTimer = AttackInterval;
            SetAgentStopped(true);
            return;
        }

        SetAgentStopped(false);
        TrySetDestination(PlayerTransform.position);
    }

    private void AttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange)
        {
            StopTentacleAttack();
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer > AttackRange)
        {
            StopTentacleAttack();
            CurrentState = EnemyState.Chase;
            SetAgentStopped(false);
            return;
        }

        Vector3 lookPosition = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
        transform.LookAt(lookPosition);

        if (_isTentacleLatched)
        {
            _latchTimer += Time.deltaTime;

            if (_playerHealthController != null)
            {
                _playerHealthController.ApplyCorrosion(
                    CorrosionDamagePerSecond,
                    CorrosionDuration,
                    CorrosionTickInterval);
            }

            if (_playerMovementController != null)
            {
                Vector3 pullTarget = TentacleOrigin != null ? TentacleOrigin.position : transform.position;
                _playerMovementController.ApplyExternalPull(pullTarget, LatchPullStrength);
            }

            if (_latchTimer >= TentacleLatchDuration)
            {
                StopTentacleAttack();
            }

            return;
        }

        if (_isTentacleStriking)
        {
            _latchTimer += Time.deltaTime;
            if (_latchTimer >= TentacleLatchDuration)
            {
                StopTentacleAttack();
            }
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= AttackInterval)
        {
            BeginTentacleStrike();
        }
    }

    private void BeginTentacleStrike()
    {
        _attackTimer = 0f;
        _latchTimer = 0f;
        _tentacleTotalDamage = 0f;
        _isTentacleStriking = true;
        _isTentacleLatched = false;
        _hasAppliedInitialLatchDamage = false;
        _hasAddedTentacleCorrosionDamage = false;
        SetTentacleHitboxEnabled(true);
    }

    private void StopTentacleAttack()
    {
        bool hadActiveTentacle = _isTentacleStriking || _isTentacleLatched;
        bool hadLatchedPlayer = _isTentacleLatched;
        float totalDamage = _tentacleTotalDamage;
        Vector3 puddlePosition = PlayerTransform != null ? PlayerTransform.position : transform.position;

        _isTentacleStriking = false;
        _isTentacleLatched = false;
        _latchTimer = 0f;
        _tentacleTotalDamage = 0f;
        _hasAppliedInitialLatchDamage = false;
        _hasAddedTentacleCorrosionDamage = false;
        SetTentacleHitboxEnabled(false);

        if (hadLatchedPlayer)
        {
            SpawnCorrosivePuddle(puddlePosition);
        }

        if (hadActiveTentacle)
        {
            EnemySkillDamageLogger.LogSkillDamage(this, "Corrosive Tentacle", totalDamage);
        }
    }

    public void NotifyTentacleHit(PlayerHealthController playerHealthController, PlayerMovementController playerMovementController)
    {
        if (!_isTentacleStriking || playerHealthController == null)
        {
            return;
        }

        _playerHealthController = playerHealthController;
        _playerMovementController = playerMovementController;
        _isTentacleLatched = true;

        if (_hasAppliedInitialLatchDamage)
        {
            return;
        }

        _hasAppliedInitialLatchDamage = true;
        _tentacleTotalDamage += _playerHealthController.TakeDamage(InitialContactDamage);
        _playerHealthController.ApplyCorrosion(
            CorrosionDamagePerSecond,
            CorrosionDuration,
            CorrosionTickInterval);

        if (!_hasAddedTentacleCorrosionDamage)
        {
            _hasAddedTentacleCorrosionDamage = true;
            _tentacleTotalDamage += CorrosionDamagePerSecond * CorrosionDuration;
        }
    }

    private void EnsureTentacleRenderer()
    {
        if (TentacleRenderer == null)
        {
            TentacleRenderer = GetComponent<LineRenderer>();
        }

        if (TentacleRenderer == null)
        {
            TentacleRenderer = gameObject.AddComponent<LineRenderer>();
        }

        TentacleRenderer.positionCount = 2;
        TentacleRenderer.enabled = false;
        TentacleRenderer.startWidth = 0.12f;
        TentacleRenderer.endWidth = 0.04f;
        TentacleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        TentacleRenderer.receiveShadows = false;
        TentacleRenderer.useWorldSpace = true;
        TentacleRenderer.numCapVertices = 4;

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null && TentacleRenderer.material == null)
        {
            TentacleRenderer.material = new Material(lineShader);
        }

        TentacleRenderer.startColor = new Color(0.43f, 0.92f, 0.56f, 1f);
        TentacleRenderer.endColor = new Color(0.08f, 0.38f, 0.12f, 0.55f);
    }

    private void EnsureTentacleHitbox()
    {
        if (_tentacleHitbox != null)
        {
            return;
        }

        GameObject hitboxObject = new GameObject("TentacleHitbox");
        hitboxObject.transform.SetParent(transform, false);
        SkillEffectLayerUtility.ApplyToRoot(hitboxObject);
        _tentacleHitbox = hitboxObject.AddComponent<ModernStranderTentacleHitbox>();
        _tentacleHitbox.Initialize(this, TentacleHitboxWidth, TentacleHitboxHeight);
        SetTentacleHitboxEnabled(false);
    }

    private void UpdateTentacleVisual()
    {
        if (TentacleRenderer == null)
        {
            return;
        }

        if ((!_isTentacleLatched && !_isTentacleStriking) || PlayerTransform == null)
        {
            TentacleRenderer.enabled = false;
            return;
        }

        TentacleRenderer.enabled = true;
        Vector3 origin = TentacleOrigin != null ? TentacleOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 target = PlayerTransform.position + Vector3.up * 1f;
        TentacleRenderer.SetPosition(0, origin);
        TentacleRenderer.SetPosition(1, target);
    }

    private void UpdateTentacleHitbox()
    {
        if (_tentacleHitbox == null || PlayerTransform == null)
        {
            return;
        }

        if (!_isTentacleStriking && !_isTentacleLatched)
        {
            _tentacleHitbox.gameObject.SetActive(false);
            return;
        }

        _tentacleHitbox.gameObject.SetActive(true);
        Vector3 origin = TentacleOrigin != null ? TentacleOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 target = PlayerTransform.position + Vector3.up * 1f;
        _tentacleHitbox.UpdateHitboxTransform(origin, target);
    }

    private void SetTentacleHitboxEnabled(bool isEnabled)
    {
        if (_tentacleHitbox == null)
        {
            return;
        }

        _tentacleHitbox.gameObject.SetActive(isEnabled);
    }

    private void SpawnCorrosivePuddle(Vector3 worldPosition)
    {
        Vector3 puddlePosition = new Vector3(worldPosition.x, 0.02f, worldPosition.z);

        if (CorrosivePuddlePrefab != null)
        {
            GameObject puddleObject = Instantiate(CorrosivePuddlePrefab, puddlePosition, Quaternion.identity);
            SkillEffectLayerUtility.ApplyToRoot(puddleObject);
            CorrosiveSlimePuddle puddle = puddleObject.GetComponent<CorrosiveSlimePuddle>();
            if (puddle != null)
            {
                puddle.Configure(PuddleRadius, PuddleLifetime, PuddleDamagePerSecond, PuddleCorrosionDuration, PuddleTickInterval);
            }
            return;
        }

        GameObject defaultPuddleObject = new GameObject("CorrosiveSlimePuddle");
        defaultPuddleObject.transform.position = puddlePosition;
        SkillEffectLayerUtility.ApplyToRoot(defaultPuddleObject);
        CorrosiveSlimePuddle defaultPuddle = defaultPuddleObject.AddComponent<CorrosiveSlimePuddle>();
        defaultPuddle.Configure(PuddleRadius, PuddleLifetime, PuddleDamagePerSecond, PuddleCorrosionDuration, PuddleTickInterval);
    }

    private void GetNewPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * PatrolRadius;
        randomDirection += _startingPosition;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, PatrolRadius, NavMesh.AllAreas))
        {
            TrySetDestination(hit.position);
        }
    }

    private bool EnsureAgentReady()
    {
        if (_navMeshAgent == null)
        {
            _navMeshAgent = GetComponent<NavMeshAgent>();
        }

        if (_navMeshAgent == null || !_navMeshAgent.enabled)
        {
            return false;
        }

        if (_navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        float searchRadius = Mathf.Max(2f, PatrolRadius);
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
        {
            _navMeshAgent.Warp(hit.position);
        }
        else
        {
            RuntimeNavMeshSurfaceBuilder.Instance?.RequestRebuild();
        }

        return _navMeshAgent.isOnNavMesh;
    }

    private bool HasReachedCurrentDestination()
    {
        return EnsureAgentReady() &&
               !_navMeshAgent.pathPending &&
               _navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance;
    }

    private void SetAgentStopped(bool isStopped)
    {
        if (EnsureAgentReady())
        {
            _navMeshAgent.isStopped = isStopped;
        }
    }

    private bool TrySetDestination(Vector3 destination)
    {
        return EnsureAgentReady() && _navMeshAgent.SetDestination(destination);
    }

    private bool EnsurePlayerReferences()
    {
        if (PlayerTransform == null)
        {
            if (PlayerHealthController.Instance != null)
            {
                PlayerTransform = PlayerHealthController.Instance.transform;
            }
            else
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    PlayerTransform = playerObject.transform;
                }
            }
        }

        if (PlayerTransform != null)
        {
            if (_playerHealthController == null)
            {
                _playerHealthController = PlayerTransform.GetComponent<PlayerHealthController>();
                if (_playerHealthController == null)
                {
                    _playerHealthController = PlayerTransform.gameObject.AddComponent<PlayerHealthController>();
                }
            }

            if (_playerMovementController == null)
            {
                _playerMovementController = PlayerTransform.GetComponent<PlayerMovementController>();
            }
        }

        if (_playerHealthController == null && PlayerHealthController.Instance != null)
        {
            _playerHealthController = PlayerHealthController.Instance;
            PlayerTransform = _playerHealthController.transform;
        }

        if (_playerMovementController == null && PlayerTransform != null)
        {
            _playerMovementController = PlayerTransform.GetComponent<PlayerMovementController>();
        }

        return PlayerTransform != null && _playerHealthController != null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, PatrolRadius);
    }
}
