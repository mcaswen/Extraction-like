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
    public float CorrosionDamagePerSecond = 10f;
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
    private bool _isTentacleStriking;
    private bool _isTentacleLatched;
    private bool _hasAppliedInitialLatchDamage;
    private ModernStranderTentacleHitbox _tentacleHitbox;

    private void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _startingPosition = transform.position;
        CurrentState = EnemyState.Patrol;

        if (PlayerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                PlayerTransform = playerObject.transform;
            }
        }

        if (PlayerTransform != null)
        {
            _playerHealthController = PlayerTransform.GetComponent<PlayerHealthController>();
            _playerMovementController = PlayerTransform.GetComponent<PlayerMovementController>();
        }

        EnsureTentacleRenderer();
        EnsureTentacleHitbox();
        GetNewPatrolPoint();
    }

    private void Update()
    {
        if (PlayerTransform == null)
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

    private void PatrolBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer <= DetectionRange)
        {
            CurrentState = EnemyState.Chase;
            return;
        }

        if (_navMeshAgent.remainingDistance <= _navMeshAgent.stoppingDistance && !_navMeshAgent.pathPending)
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
            _navMeshAgent.isStopped = false;
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer <= AttackRange)
        {
            CurrentState = EnemyState.Attack;
            _attackTimer = AttackInterval;
            _navMeshAgent.isStopped = true;
            return;
        }

        _navMeshAgent.isStopped = false;
        _navMeshAgent.SetDestination(PlayerTransform.position);
    }

    private void AttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange)
        {
            StopTentacleAttack();
            CurrentState = EnemyState.Patrol;
            _navMeshAgent.isStopped = false;
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer > AttackRange)
        {
            StopTentacleAttack();
            CurrentState = EnemyState.Chase;
            _navMeshAgent.isStopped = false;
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
        _isTentacleStriking = true;
        _isTentacleLatched = false;
        _hasAppliedInitialLatchDamage = false;
        SetTentacleHitboxEnabled(true);
    }

    private void StopTentacleAttack()
    {
        bool hadLatchedPlayer = _isTentacleLatched;
        Vector3 puddlePosition = PlayerTransform != null ? PlayerTransform.position : transform.position;

        _isTentacleStriking = false;
        _isTentacleLatched = false;
        _latchTimer = 0f;
        _hasAppliedInitialLatchDamage = false;
        SetTentacleHitboxEnabled(false);

        if (hadLatchedPlayer)
        {
            SpawnCorrosivePuddle(puddlePosition);
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
        _playerHealthController.TakeDamage(InitialContactDamage);
        _playerHealthController.ApplyCorrosion(
            CorrosionDamagePerSecond,
            CorrosionDuration,
            CorrosionTickInterval);
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
            CorrosiveSlimePuddle puddle = puddleObject.GetComponent<CorrosiveSlimePuddle>();
            if (puddle != null)
            {
                puddle.Configure(PuddleRadius, PuddleLifetime, PuddleDamagePerSecond, PuddleCorrosionDuration, PuddleTickInterval);
            }
            return;
        }

        GameObject defaultPuddleObject = new GameObject("CorrosiveSlimePuddle");
        defaultPuddleObject.transform.position = puddlePosition;
        CorrosiveSlimePuddle defaultPuddle = defaultPuddleObject.AddComponent<CorrosiveSlimePuddle>();
        defaultPuddle.Configure(PuddleRadius, PuddleLifetime, PuddleDamagePerSecond, PuddleCorrosionDuration, PuddleTickInterval);
    }

    private void GetNewPatrolPoint()
    {
        Vector3 randomDirection = Random.insideUnitSphere * PatrolRadius;
        randomDirection += _startingPosition;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, PatrolRadius, NavMesh.AllAreas))
        {
            _navMeshAgent.SetDestination(hit.position);
        }
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
