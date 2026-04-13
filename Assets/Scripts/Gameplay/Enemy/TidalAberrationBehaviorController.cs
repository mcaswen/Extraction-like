using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 潮汐畸变体白模行为。
/// 近距离使用水母触须电击玩家，远距离喷射高压水柱击退玩家。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class TidalAberrationBehaviorController : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        MeleeAttack,
        RangedAttack
    }

    public EnemyState CurrentState;

    [Header("References")]
    public Transform PlayerTransform;
    public Transform MeleeOrigin;
    public Transform RangedOrigin;
    public LineRenderer ElectricTentacleRenderer;
    public LineRenderer WaterJetRenderer;

    [Header("Patrol")]
    public float PatrolRadius = 8f;
    public float PatrolWaitTime = 1.5f;

    [Header("Detection")]
    public float DetectionRange = 14f;
    public float LoseRange = 18f;

    [Header("Melee Attack")]
    public float MeleeAttackRange = 3f;
    public float MeleeAttackInterval = 2.2f;
    public float MeleeLatchDuration = 0.8f;
    public float MeleeContactDamage = 10f;
    public float SilenceDuration = 1.5f;
    public float ElectricTickDamagePerSecond = 4f;
    public float ElectricTickInterval = 0.25f;

    [Header("Ranged Attack")]
    public float MinimumRangedDistance = 4.5f;
    public float RangedAttackRange = 9f;
    public float RangedAttackInterval = 2.6f;
    public float WaterJetDuration = 0.18f;
    public float WaterJetDamage = 14f;
    public float WaterJetKnockbackStrength = 5.2f;
    public float WaterJetMaxDistance = 10f;

    private NavMeshAgent _navMeshAgent;
    private PlayerHealthController _playerHealthController;
    private PlayerMovementController _playerMovementController;
    private PlayerShootingController _playerShootingController;
    private Vector3 _startingPosition;
    private float _waitTimer;
    private float _meleeAttackTimer;
    private float _rangedAttackTimer;
    private float _meleeVisualTimer;
    private float _rangedVisualTimer;
    private bool _isMeleeLatched;
    private bool _isRangedCasting;

    private void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _startingPosition = transform.position;
        CurrentState = EnemyState.Patrol;
        float effectiveMeleeRange = GetEffectiveMeleeRange();
        if (MinimumRangedDistance <= effectiveMeleeRange + 0.75f)
        {
            MinimumRangedDistance = effectiveMeleeRange + 1.2f;
        }
        if (EnsureAgentReady())
        {
            _navMeshAgent.stoppingDistance = Mathf.Max(0.2f, effectiveMeleeRange * 0.9f);
        }
        EnsurePlayerReferences();

        EnsureLineRenderers();
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
            case EnemyState.MeleeAttack:
                MeleeAttackBehavior(distanceToPlayer);
                break;
            case EnemyState.RangedAttack:
                RangedAttackBehavior(distanceToPlayer);
                break;
        }

        UpdateAttackVisuals();
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
        float effectiveMeleeRange = GetEffectiveMeleeRange();

        if (distanceToPlayer > LoseRange)
        {
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer <= effectiveMeleeRange)
        {
            CurrentState = EnemyState.MeleeAttack;
            _meleeAttackTimer = MeleeAttackInterval;
            SetAgentStopped(true);
            return;
        }

        if (distanceToPlayer >= MinimumRangedDistance && distanceToPlayer <= RangedAttackRange)
        {
            CurrentState = EnemyState.RangedAttack;
            _rangedAttackTimer = RangedAttackInterval;
            SetAgentStopped(true);
            return;
        }

        SetAgentStopped(false);
        TrySetDestination(PlayerTransform.position);
    }

    private void MeleeAttackBehavior(float distanceToPlayer)
    {
        float effectiveMeleeRange = GetEffectiveMeleeRange();

        if (distanceToPlayer > LoseRange)
        {
            StopMeleeAttack();
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer > RangedAttackRange)
        {
            StopMeleeAttack();
            CurrentState = EnemyState.Chase;
            _navMeshAgent.isStopped = false;
            return;
        }

        if (distanceToPlayer > effectiveMeleeRange)
        {
            StopMeleeAttack();
            CurrentState = EnemyState.RangedAttack;
            _rangedAttackTimer = RangedAttackInterval;
            return;
        }

        LookAtPlayer();

        if (_isMeleeLatched)
        {
            _meleeVisualTimer += Time.deltaTime;
            if (_playerHealthController != null)
            {
                _playerHealthController.TakeDamage(ElectricTickDamagePerSecond * ElectricTickInterval);
            }

            if (_meleeVisualTimer >= MeleeLatchDuration)
            {
                StopMeleeAttack();
            }
            return;
        }

        _meleeAttackTimer += Time.deltaTime;
        if (_meleeAttackTimer >= MeleeAttackInterval)
        {
            PerformMeleeAttack();
        }
    }

    private void RangedAttackBehavior(float distanceToPlayer)
    {
        float effectiveMeleeRange = GetEffectiveMeleeRange();

        if (distanceToPlayer > LoseRange)
        {
            StopRangedAttack();
            CurrentState = EnemyState.Patrol;
            SetAgentStopped(false);
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer <= effectiveMeleeRange)
        {
            StopRangedAttack();
            CurrentState = EnemyState.MeleeAttack;
            _meleeAttackTimer = MeleeAttackInterval;
            return;
        }

        if (distanceToPlayer < MinimumRangedDistance || distanceToPlayer > RangedAttackRange)
        {
            StopRangedAttack();
            CurrentState = EnemyState.Chase;
            SetAgentStopped(false);
            return;
        }

        LookAtPlayer();

        if (_isRangedCasting)
        {
            _rangedVisualTimer += Time.deltaTime;
            if (_rangedVisualTimer >= WaterJetDuration)
            {
                StopRangedAttack();
            }
            return;
        }

        _rangedAttackTimer += Time.deltaTime;
        if (_rangedAttackTimer >= RangedAttackInterval)
        {
            PerformRangedAttack();
        }
    }

    private void PerformMeleeAttack()
    {
        _meleeAttackTimer = 0f;
        _meleeVisualTimer = 0f;
        _isMeleeLatched = true;

        if (_playerHealthController != null)
        {
            _playerHealthController.TakeDamage(MeleeContactDamage);
        }

        if (_playerShootingController != null)
        {
            _playerShootingController.ApplySilence(SilenceDuration);
        }

        Debug.Log($"[{name}] 近战电击命中玩家。");
    }

    private void StopMeleeAttack()
    {
        _isMeleeLatched = false;
        _meleeVisualTimer = 0f;
    }

    private void PerformRangedAttack()
    {
        _rangedAttackTimer = 0f;
        _rangedVisualTimer = 0f;
        _isRangedCasting = true;

        Vector3 origin = RangedOrigin != null ? RangedOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 direction = (PlayerTransform.position + Vector3.up * 0.8f) - origin;
        direction.Normalize();

        if (Physics.Raycast(origin, direction, out RaycastHit hit, WaterJetMaxDistance))
        {
            if (hit.collider.CompareTag("Player"))
            {
                if (_playerHealthController != null)
                {
                    _playerHealthController.TakeDamage(WaterJetDamage);
                }

                if (_playerMovementController != null)
                {
                    _playerMovementController.ApplyExternalImpulse(direction, WaterJetKnockbackStrength);
                }
            }
        }
    }

    private void StopRangedAttack()
    {
        _isRangedCasting = false;
        _rangedVisualTimer = 0f;
    }

    private void EnsureLineRenderers()
    {
        if (ElectricTentacleRenderer == null)
        {
            ElectricTentacleRenderer = CreateLineRenderer("ElectricTentacle", new Color(1f, 0.9f, 0.32f, 0.95f), new Color(1f, 0.64f, 0.08f, 0.55f), 0.09f, 0.04f);
        }

        if (WaterJetRenderer == null)
        {
            WaterJetRenderer = CreateLineRenderer("WaterJet", new Color(0.72f, 0.94f, 1f, 0.95f), new Color(0.35f, 0.7f, 1f, 0.45f), 0.12f, 0.06f);
        }
    }

    private LineRenderer CreateLineRenderer(string objectName, Color startColor, Color endColor, float startWidth, float endWidth)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.useWorldSpace = true;
        lineRenderer.numCapVertices = 4;

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null)
        {
            lineRenderer.material = new Material(lineShader);
        }

        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        return lineRenderer;
    }

    private void UpdateAttackVisuals()
    {
        UpdateMeleeVisual();
        UpdateRangedVisual();
    }

    private void UpdateMeleeVisual()
    {
        if (ElectricTentacleRenderer == null)
        {
            return;
        }

        if (!_isMeleeLatched || PlayerTransform == null)
        {
            ElectricTentacleRenderer.enabled = false;
            return;
        }

        ElectricTentacleRenderer.enabled = true;
        float pulse = 0.5f + Mathf.Sin(Time.time * 24f) * 0.5f;
        ElectricTentacleRenderer.startWidth = 0.14f + pulse * 0.05f;
        ElectricTentacleRenderer.endWidth = 0.06f + pulse * 0.03f;
        ElectricTentacleRenderer.startColor = Color.Lerp(new Color(1f, 0.92f, 0.35f, 0.92f), Color.white, pulse * 0.55f);
        ElectricTentacleRenderer.endColor = new Color(1f, 0.62f, 0.08f, 0.65f);
        Vector3 origin = MeleeOrigin != null ? MeleeOrigin.position : transform.position + Vector3.up * 1.15f;
        Vector3 target = PlayerTransform.position + Vector3.up * 0.95f;
        ElectricTentacleRenderer.SetPosition(0, origin);
        ElectricTentacleRenderer.SetPosition(1, target);
    }

    private void UpdateRangedVisual()
    {
        if (WaterJetRenderer == null)
        {
            return;
        }

        if (!_isRangedCasting || PlayerTransform == null)
        {
            WaterJetRenderer.enabled = false;
            return;
        }

        WaterJetRenderer.enabled = true;
        Vector3 origin = RangedOrigin != null ? RangedOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 target = PlayerTransform.position + Vector3.up * 0.85f;
        WaterJetRenderer.SetPosition(0, origin);
        WaterJetRenderer.SetPosition(1, target);
    }

    private void LookAtPlayer()
    {
        Vector3 lookPosition = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
        transform.LookAt(lookPosition);
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

            if (_playerShootingController == null)
            {
                _playerShootingController = PlayerTransform.GetComponent<PlayerShootingController>();
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

        if (_playerShootingController == null && PlayerTransform != null)
        {
            _playerShootingController = PlayerTransform.GetComponent<PlayerShootingController>();
        }

        return PlayerTransform != null && _playerHealthController != null;
    }

    private void OnDrawGizmosSelected()
    {
        float effectiveMeleeRange = Mathf.Max(MeleeAttackRange, 4f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 1f);
        Gizmos.DrawWireSphere(transform.position, effectiveMeleeRange);
        Gizmos.color = new Color(0.15f, 0.95f, 0.95f, 1f);
        Gizmos.DrawWireSphere(transform.position, MinimumRangedDistance);
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
        Gizmos.DrawWireSphere(transform.position, RangedAttackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, PatrolRadius);
    }

    private float GetEffectiveMeleeRange()
    {
        return Mathf.Max(MeleeAttackRange, 4f);
    }
}
