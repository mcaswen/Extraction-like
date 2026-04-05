using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 古代搁浅者白模行为。
/// 近战挥舞鱼骨造成小范围伤害，中距离延伸鱼骨并撕咬玩家。
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AncientStranderBehaviorController : MonoBehaviour
{
    public enum EnemyState
    {
        Patrol,
        Chase,
        MeleeAttack,
        RangedBiteAttack
    }

    public EnemyState CurrentState;

    [Header("References")]
    public Transform PlayerTransform;
    public Transform MeleeOrigin;
    public Transform BiteOrigin;
    public LineRenderer MeleeSwingRenderer;
    public LineRenderer FishboneBiteRenderer;

    [Header("Patrol")]
    public float PatrolRadius = 8f;
    public float PatrolWaitTime = 1.4f;

    [Header("Detection")]
    public float DetectionRange = 13f;
    public float LoseRange = 17f;

    [Header("Melee Fishbone Sweep")]
    public float MeleeAttackRange = 2.8f;
    public float MeleeAttackInterval = 1.8f;
    public float MeleeAttackRadius = 1.9f;
    public float MeleeDamage = 12f;
    public float MeleeVisualDuration = 0.2f;

    [Header("Ranged Fishbone Bite")]
    public float MinimumRangedDistance = 3.4f;
    public float RangedAttackRange = 7.6f;
    public float RangedAttackInterval = 2.4f;
    public float BiteStrikeDuration = 0.42f;
    public float BiteHitboxWidth = 0.42f;
    public float BiteHitboxHeight = 0.42f;
    public float BiteDamage = 15f;

    private NavMeshAgent _navMeshAgent;
    private PlayerHealthController _playerHealthController;
    private Vector3 _startingPosition;
    private float _waitTimer;
    private float _meleeAttackTimer;
    private float _rangedAttackTimer;
    private float _meleeVisualTimer;
    private float _biteStrikeTimer;
    private bool _isBiteStriking;
    private bool _hasAppliedBiteDamage;
    private AncientStranderBiteHitbox _biteHitbox;

    private void Start()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _startingPosition = transform.position;
        CurrentState = EnemyState.Patrol;
        _navMeshAgent.stoppingDistance = Mathf.Max(0.2f, MeleeAttackRange * 0.85f);

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
        }

        if (MinimumRangedDistance <= MeleeAttackRange + 0.5f)
        {
            MinimumRangedDistance = MeleeAttackRange + 1f;
        }

        EnsureLineRenderers();
        EnsureBiteHitbox();
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
            case EnemyState.MeleeAttack:
                MeleeAttackBehavior(distanceToPlayer);
                break;
            case EnemyState.RangedBiteAttack:
                RangedBiteAttackBehavior(distanceToPlayer);
                break;
        }

        UpdateMeleeVisual();
        UpdateBiteVisual();
        UpdateBiteHitbox();
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

        if (distanceToPlayer <= MeleeAttackRange)
        {
            CurrentState = EnemyState.MeleeAttack;
            _meleeAttackTimer = MeleeAttackInterval;
            _navMeshAgent.isStopped = true;
            return;
        }

        if (distanceToPlayer >= MinimumRangedDistance && distanceToPlayer <= RangedAttackRange)
        {
            CurrentState = EnemyState.RangedBiteAttack;
            _rangedAttackTimer = RangedAttackInterval;
            _navMeshAgent.isStopped = true;
            return;
        }

        _navMeshAgent.isStopped = false;
        _navMeshAgent.SetDestination(PlayerTransform.position);
    }

    private void MeleeAttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange)
        {
            CurrentState = EnemyState.Patrol;
            _navMeshAgent.isStopped = false;
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer > MeleeAttackRange + 1f)
        {
            CurrentState = EnemyState.Chase;
            _navMeshAgent.isStopped = false;
            return;
        }

        LookAtPlayer();
        _meleeAttackTimer += Time.deltaTime;
        if (_meleeAttackTimer >= MeleeAttackInterval)
        {
            _meleeAttackTimer = 0f;
            PerformMeleeAttack();
        }
    }

    private void RangedBiteAttackBehavior(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange)
        {
            StopBiteStrike();
            CurrentState = EnemyState.Patrol;
            _navMeshAgent.isStopped = false;
            GetNewPatrolPoint();
            return;
        }

        if (distanceToPlayer <= MeleeAttackRange)
        {
            StopBiteStrike();
            CurrentState = EnemyState.MeleeAttack;
            _meleeAttackTimer = MeleeAttackInterval;
            return;
        }

        if (distanceToPlayer < MinimumRangedDistance || distanceToPlayer > RangedAttackRange)
        {
            StopBiteStrike();
            CurrentState = EnemyState.Chase;
            _navMeshAgent.isStopped = false;
            return;
        }

        LookAtPlayer();

        if (_isBiteStriking)
        {
            _biteStrikeTimer += Time.deltaTime;
            if (_biteStrikeTimer >= BiteStrikeDuration)
            {
                StopBiteStrike();
            }
            return;
        }

        _rangedAttackTimer += Time.deltaTime;
        if (_rangedAttackTimer >= RangedAttackInterval)
        {
            BeginBiteStrike();
        }
    }

    private void PerformMeleeAttack()
    {
        _meleeVisualTimer = MeleeVisualDuration;
        Vector3 center = MeleeOrigin != null ? MeleeOrigin.position : transform.position + transform.forward * 1.2f;
        Collider[] hits = Physics.OverlapSphere(center, MeleeAttackRadius);
        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Player"))
            {
                continue;
            }

            PlayerHealthController playerHealth = hit.GetComponentInParent<PlayerHealthController>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(MeleeDamage);
            }
        }
    }

    private void BeginBiteStrike()
    {
        _rangedAttackTimer = 0f;
        _biteStrikeTimer = 0f;
        _isBiteStriking = true;
        _hasAppliedBiteDamage = false;
        SetBiteHitboxEnabled(true);
    }

    public void NotifyBiteHit(PlayerHealthController playerHealth)
    {
        if (!_isBiteStriking || _hasAppliedBiteDamage || playerHealth == null)
        {
            return;
        }

        _hasAppliedBiteDamage = true;
        playerHealth.TakeDamage(BiteDamage);
    }

    private void StopBiteStrike()
    {
        _isBiteStriking = false;
        _biteStrikeTimer = 0f;
        _hasAppliedBiteDamage = false;
        SetBiteHitboxEnabled(false);
    }

    private void EnsureLineRenderers()
    {
        if (MeleeSwingRenderer == null)
        {
            MeleeSwingRenderer = CreateLineRenderer("FishboneMeleeSwing", new Color(0.95f, 0.92f, 0.78f, 0.92f), new Color(0.78f, 0.76f, 0.6f, 0.38f), 0.14f, 0.04f);
        }

        if (FishboneBiteRenderer == null)
        {
            FishboneBiteRenderer = CreateLineRenderer("FishboneBite", new Color(0.92f, 0.96f, 1f, 0.95f), new Color(0.75f, 0.82f, 0.92f, 0.42f), 0.1f, 0.035f);
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
        lineRenderer.useWorldSpace = true;
        lineRenderer.numCapVertices = 4;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null)
        {
            lineRenderer.material = new Material(lineShader);
        }

        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        return lineRenderer;
    }

    private void EnsureBiteHitbox()
    {
        if (_biteHitbox != null)
        {
            return;
        }

        GameObject hitboxObject = new GameObject("FishboneBiteHitbox");
        hitboxObject.transform.SetParent(transform, false);
        _biteHitbox = hitboxObject.AddComponent<AncientStranderBiteHitbox>();
        _biteHitbox.Initialize(this, BiteHitboxWidth, BiteHitboxHeight);
        SetBiteHitboxEnabled(false);
    }

    private void SetBiteHitboxEnabled(bool isEnabled)
    {
        if (_biteHitbox == null)
        {
            return;
        }

        _biteHitbox.gameObject.SetActive(isEnabled);
    }

    private void UpdateBiteHitbox()
    {
        if (_biteHitbox == null || PlayerTransform == null)
        {
            return;
        }

        if (!_isBiteStriking)
        {
            _biteHitbox.gameObject.SetActive(false);
            return;
        }

        _biteHitbox.gameObject.SetActive(true);
        Vector3 origin = BiteOrigin != null ? BiteOrigin.position : transform.position + Vector3.up * 1.1f;
        Vector3 target = PlayerTransform.position + Vector3.up * 0.9f;
        _biteHitbox.UpdateHitboxTransform(origin, target);
    }

    private void UpdateMeleeVisual()
    {
        if (MeleeSwingRenderer == null)
        {
            return;
        }

        if (_meleeVisualTimer <= 0f)
        {
            MeleeSwingRenderer.enabled = false;
            return;
        }

        _meleeVisualTimer -= Time.deltaTime;
        MeleeSwingRenderer.enabled = true;
        float normalizedTime = 1f - (_meleeVisualTimer / Mathf.Max(0.01f, MeleeVisualDuration));
        float sweepAngle = Mathf.Lerp(-55f, 55f, normalizedTime);
        Vector3 origin = MeleeOrigin != null ? MeleeOrigin.position : transform.position + Vector3.up * 1.05f;
        Vector3 direction = Quaternion.Euler(0f, sweepAngle, 0f) * transform.forward;
        Vector3 endPoint = origin + direction * MeleeAttackRadius;

        MeleeSwingRenderer.SetPosition(0, origin);
        MeleeSwingRenderer.SetPosition(1, endPoint);
    }

    private void UpdateBiteVisual()
    {
        if (FishboneBiteRenderer == null)
        {
            return;
        }

        if (!_isBiteStriking || PlayerTransform == null)
        {
            FishboneBiteRenderer.enabled = false;
            return;
        }

        FishboneBiteRenderer.enabled = true;
        Vector3 origin = BiteOrigin != null ? BiteOrigin.position : transform.position + Vector3.up * 1.1f;
        Vector3 target = PlayerTransform.position + Vector3.up * 0.9f;
        FishboneBiteRenderer.SetPosition(0, origin);
        FishboneBiteRenderer.SetPosition(1, target);
        float pulse = 0.5f + Mathf.Sin(Time.time * 20f) * 0.5f;
        FishboneBiteRenderer.startWidth = 0.09f + pulse * 0.04f;
        FishboneBiteRenderer.endWidth = 0.035f + pulse * 0.015f;
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
            _navMeshAgent.SetDestination(hit.position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);
        Gizmos.color = new Color(0.85f, 0.82f, 0.65f, 1f);
        Gizmos.DrawWireSphere(transform.position, MeleeAttackRange);
        Gizmos.color = new Color(0.62f, 0.75f, 0.88f, 1f);
        Gizmos.DrawWireSphere(transform.position, MinimumRangedDistance);
        Gizmos.color = new Color(0.32f, 0.5f, 0.7f, 1f);
        Gizmos.DrawWireSphere(transform.position, RangedAttackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, PatrolRadius);
    }
}

/// <summary>
/// 古代搁浅者远程鱼骨撕咬命中盒。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class AncientStranderBiteHitbox : MonoBehaviour
{
    private AncientStranderBehaviorController _owner;
    private BoxCollider _boxCollider;
    private float _width;
    private float _height;

    public void Initialize(AncientStranderBehaviorController owner, float width, float height)
    {
        _owner = owner;
        _width = Mathf.Max(0.1f, width);
        _height = Mathf.Max(0.1f, height);

        _boxCollider = GetComponent<BoxCollider>();
        _boxCollider.isTrigger = true;
    }

    public void UpdateHitboxTransform(Vector3 origin, Vector3 target)
    {
        if (_boxCollider == null)
        {
            _boxCollider = GetComponent<BoxCollider>();
            _boxCollider.isTrigger = true;
        }

        Vector3 delta = target - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return;
        }

        transform.position = origin + delta * 0.5f;
        transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        _boxCollider.size = new Vector3(_width, _height, distance);
        _boxCollider.center = Vector3.zero;
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyOwner(other);
    }

    private void OnTriggerStay(Collider other)
    {
        NotifyOwner(other);
    }

    private void NotifyOwner(Collider other)
    {
        if (_owner == null || !other.CompareTag("Player"))
        {
            return;
        }

        PlayerHealthController playerHealth = other.GetComponentInParent<PlayerHealthController>();
        _owner.NotifyBiteHit(playerHealth);
    }
}
