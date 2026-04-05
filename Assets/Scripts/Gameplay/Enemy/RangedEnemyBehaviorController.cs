using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Basic ranged enemy behaviour with patrol, chase and ranged attack.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class RangedEnemyBehaviorController : MonoBehaviour
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
    public Transform FirePoint;
    public GameObject EnemyBulletPrefab;

    [Header("Patrol")]
    public float PatrolRadius = 10f;
    public float PatrolWaitTime = 2f;

    [Header("Detection")]
    public float DetectionRange = 15f;
    public float LoseRange = 20f;

    [Header("Attack")]
    public float AttackRange = 10f;
    public float AttackInterval = 2f;

    private NavMeshAgent _navMeshAgent;
    private Vector3 _startingPosition;
    private float _waitTimer;
    private float _attackTimer;

    private void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _startingPosition = transform.position;
        CurrentState = EnemyState.Patrol;
        EnsureAgentReady();

        if (PlayerTransform == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                PlayerTransform = playerObject.transform;
            }
        }

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
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer <= AttackRange)
        {
            CurrentState = EnemyState.Attack;
            SetAgentStopped(true);
            return;
        }

        SetAgentStopped(false);
        TrySetDestination(PlayerTransform.position);
    }

    private void AttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > AttackRange)
        {
            CurrentState = EnemyState.Chase;
            SetAgentStopped(false);
            return;
        }

        Vector3 lookPosition = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
        transform.LookAt(lookPosition);

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= AttackInterval)
        {
            Shoot();
            _attackTimer = 0f;
        }
    }

    private void Shoot()
    {
        if (FirePoint == null || EnemyBulletPrefab == null)
        {
            return;
        }

        Instantiate(EnemyBulletPrefab, FirePoint.position, FirePoint.rotation);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, PatrolRadius);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }
}
