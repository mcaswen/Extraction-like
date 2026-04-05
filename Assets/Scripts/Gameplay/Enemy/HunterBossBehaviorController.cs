using UnityEngine;

/// <summary>
/// 追猎者白模 Boss 行为。
/// 近战挥锚、漩涡控场后抛锚、低血怒吼音波。
/// </summary>
public class HunterBossBehaviorController : MonoBehaviour
{
    public enum BossState
    {
        Idle,
        Chase,
        MeleeAttack,
        VortexAttack,
        RoarAttack,
        Cooldown
    }

    public BossState CurrentState;

    [Header("References")]
    public Transform PlayerTransform;
    public Transform MeleeOrigin;
    public Transform ProjectileOrigin;
    public Transform EyeOrigin;
    public GameObject AnchorProjectilePrefab;
    public GameObject VortexFieldPrefab;
    public LineRenderer MeleeSwingRenderer;
    public LineRenderer RoarWaveRenderer;

    [Header("Movement")]
    public float DetectionRange = 18f;
    public float LoseRange = 24f;
    public float ChaseSpeed = 3.5f;

    [Header("Melee Anchor Sweep")]
    public float MeleeAttackRange = 4f;
    public float MeleeAttackInterval = 2.4f;
    public float MeleeAttackRadius = 2.2f;
    public float MeleeDamage = 18f;
    public float MeleeKnockbackStrength = 6f;
    public float MeleeVisualDuration = 0.22f;

    [Header("Vortex + Anchor Throw")]
    public float VortexTriggerDistance = 10f;
    public float VortexRadius = 3f;
    public float VortexChargeDuration = 1.4f;
    public float VortexImmobilizeDuration = 0.4f;
    public float AnchorThrowSpeed = 14f;
    public float AnchorThrowDamage = 16f;
    public float AnchorThrowKnockback = 5.5f;
    public float AnchorThrowCooldown = 3f;

    [Header("Roar")]
    public float RageThreshold = 0.5f;
    public float RoarChargeDuration = 1.2f;
    public float RoarCooldown = 7f;
    public float RoarRange = 12f;
    public float RoarDamage = 999f;
    public LayerMask CoverMask;
    public float CoverCheckHeight = 1.1f;

    private EnemyHealthController _healthController;
    private PlayerHealthController _playerHealthController;
    private PlayerMovementController _playerMovementController;
    private float _meleeTimer;
    private float _vortexTimer;
    private float _cooldownTimer;
    private float _currentCooldownDuration;
    private float _roarTimer;
    private float _meleeVisualTimer;
    private bool _hasTriggeredRageRoar;
    private HunterBossVortexField _activeVortexField;

    private void Start()
    {
        _healthController = GetComponent<EnemyHealthController>();

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

        EnsureLineRenderers();
        CurrentState = BossState.Idle;
    }

    private void Update()
    {
        if (PlayerTransform == null || _playerHealthController == null)
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
        HandleRageRoar(distanceToPlayer);

        switch (CurrentState)
        {
            case BossState.Idle:
                TickIdle(distanceToPlayer);
                break;
            case BossState.Chase:
                TickChase(distanceToPlayer);
                break;
            case BossState.MeleeAttack:
                TickMelee(distanceToPlayer);
                break;
            case BossState.VortexAttack:
                TickVortex(distanceToPlayer);
                break;
            case BossState.RoarAttack:
                TickRoar(distanceToPlayer);
                break;
            case BossState.Cooldown:
                TickCooldown(distanceToPlayer);
                break;
        }

        UpdateBossVisuals();
    }

    private void TickIdle(float distanceToPlayer)
    {
        if (distanceToPlayer <= DetectionRange)
        {
            CurrentState = BossState.Chase;
        }
    }

    private void TickChase(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange)
        {
            return;
        }

        LookAtPlayer();
        MoveTowardsPlayer();

        if (distanceToPlayer <= MeleeAttackRange)
        {
            CurrentState = BossState.MeleeAttack;
            _meleeTimer = MeleeAttackInterval;
            return;
        }

        if (distanceToPlayer <= VortexTriggerDistance)
        {
            CurrentState = BossState.VortexAttack;
            _vortexTimer = 0f;
            SpawnOrMoveVortexField();
        }
    }

    private void TickMelee(float distanceToPlayer)
    {
        if (distanceToPlayer > MeleeAttackRange + 1f)
        {
            CurrentState = BossState.Chase;
            return;
        }

        LookAtPlayer();
        _meleeTimer += Time.deltaTime;
        if (_meleeTimer >= MeleeAttackInterval)
        {
            _meleeTimer = 0f;
            PerformMeleeAttack();
            EnterCooldown(1.1f);
        }
    }

    private void TickVortex(float distanceToPlayer)
    {
        if (distanceToPlayer > LoseRange)
        {
            ClearVortexField();
            CurrentState = BossState.Chase;
            return;
        }

        LookAtPlayer();
        _vortexTimer += Time.deltaTime;

        if (_activeVortexField != null && _vortexTimer >= VortexChargeDuration * 0.4f)
        {
            _activeVortexField.Arm();
        }

        if (_vortexTimer >= VortexChargeDuration)
        {
            ThrowAnchorProjectile();
            ClearVortexField();
            EnterCooldown(AnchorThrowCooldown);
        }
    }

    private void TickRoar(float distanceToPlayer)
    {
        LookAtPlayer();
        _roarTimer += Time.deltaTime;

        if (_roarTimer >= RoarChargeDuration)
        {
            ExecuteRoar(distanceToPlayer);
            EnterCooldown(RoarCooldown);
        }
    }

    private void TickCooldown(float distanceToPlayer)
    {
        _cooldownTimer += Time.deltaTime;
        if (_cooldownTimer >= _currentCooldownDuration)
        {
            if (distanceToPlayer <= MeleeAttackRange)
            {
                CurrentState = BossState.MeleeAttack;
                _meleeTimer = 0f;
                return;
            }

            CurrentState = BossState.Chase;
        }
    }

    private void PerformMeleeAttack()
    {
        _meleeVisualTimer = MeleeVisualDuration;
        Vector3 center = MeleeOrigin != null ? MeleeOrigin.position : transform.position + transform.forward * 1.4f;
        Collider[] hits = Physics.OverlapSphere(center, MeleeAttackRadius);
        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Player"))
            {
                continue;
            }

            PlayerHealthController playerHealth = hit.GetComponentInParent<PlayerHealthController>();
            PlayerMovementController playerMovement = hit.GetComponentInParent<PlayerMovementController>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(MeleeDamage);
            }

            if (playerMovement != null)
            {
                Vector3 pushDirection = hit.transform.position - transform.position;
                playerMovement.ApplyExternalImpulse(pushDirection, MeleeKnockbackStrength);
            }
        }
    }

    private void SpawnOrMoveVortexField()
    {
        Vector3 vortexPosition = PlayerTransform.position;
        vortexPosition.y = 0.02f;

        if (_activeVortexField == null)
        {
            if (VortexFieldPrefab != null)
            {
                GameObject vortexObject = Instantiate(VortexFieldPrefab, vortexPosition, Quaternion.identity);
                _activeVortexField = vortexObject.GetComponent<HunterBossVortexField>();
            }

            if (_activeVortexField == null)
            {
                GameObject vortexObject = new GameObject("HunterBossVortexField");
                vortexObject.transform.position = vortexPosition;
                _activeVortexField = vortexObject.AddComponent<HunterBossVortexField>();
            }

            _activeVortexField.Configure(VortexRadius, VortexChargeDuration + 0.4f, VortexImmobilizeDuration, false);
        }
        else
        {
            _activeVortexField.transform.position = vortexPosition;
        }
    }

    private void ThrowAnchorProjectile()
    {
        if (ProjectileOrigin == null)
        {
            return;
        }

        Vector3 direction = (PlayerTransform.position + Vector3.up * 0.8f) - ProjectileOrigin.position;
        direction.Normalize();

        GameObject projectileObject = CreateAnchorProjectile(direction);
        if (projectileObject == null)
        {
            return;
        }

        HunterBossAnchorProjectile projectile = projectileObject.GetComponent<HunterBossAnchorProjectile>();
        if (projectile != null)
        {
            projectile.Damage = AnchorThrowDamage;
            projectile.KnockbackStrength = AnchorThrowKnockback;
            projectile.Launch(direction * AnchorThrowSpeed);
        }
    }

    private void HandleRageRoar(float distanceToPlayer)
    {
        if (_hasTriggeredRageRoar || _healthController == null || _healthController.MaxHealth <= 0f)
        {
            return;
        }

        float healthRatio = _healthController.GetCurrentHealthRatio();
        if (healthRatio > RageThreshold)
        {
            return;
        }

        _hasTriggeredRageRoar = true;
        CurrentState = BossState.RoarAttack;
        _roarTimer = 0f;
        ClearVortexField();
    }

    private void ExecuteRoar(float distanceToPlayer)
    {
        if (_playerHealthController == null || distanceToPlayer > RoarRange)
        {
            return;
        }

        if (IsPlayerProtectedByCover())
        {
            _playerHealthController.TakeDamage(RoarDamage * 0.1f);
            return;
        }

        _playerHealthController.TakeDamage(RoarDamage);
    }

    private bool IsPlayerProtectedByCover()
    {
        if (PlayerTransform == null || EyeOrigin == null)
        {
            return false;
        }

        Vector3 origin = EyeOrigin.position;
        Vector3 target = PlayerTransform.position + Vector3.up * CoverCheckHeight;
        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
        {
            return false;
        }

        return Physics.Raycast(origin, direction.normalized, distance, CoverMask);
    }

    private void EnterCooldown(float duration)
    {
        CurrentState = BossState.Cooldown;
        _cooldownTimer = 0f;
        _currentCooldownDuration = Mathf.Max(0.25f, duration);
    }

    private void MoveTowardsPlayer()
    {
        Vector3 direction = PlayerTransform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        transform.position += direction.normalized * ChaseSpeed * Time.deltaTime;
    }

    private void LookAtPlayer()
    {
        Vector3 lookPosition = new Vector3(PlayerTransform.position.x, transform.position.y, PlayerTransform.position.z);
        transform.LookAt(lookPosition);
    }

    private void EnsureLineRenderers()
    {
        if (MeleeSwingRenderer == null)
        {
            MeleeSwingRenderer = CreateLineRenderer("MeleeSwing", new Color(1f, 0.86f, 0.28f, 0.95f), new Color(1f, 0.45f, 0.12f, 0.45f), 0.18f, 0.04f);
        }

        if (RoarWaveRenderer != null)
        {
            return;
        }

        GameObject lineObject = new GameObject("RoarWave");
        lineObject.transform.SetParent(transform, false);
        RoarWaveRenderer = lineObject.AddComponent<LineRenderer>();
        RoarWaveRenderer.positionCount = 2;
        RoarWaveRenderer.enabled = false;
        RoarWaveRenderer.startWidth = 0.2f;
        RoarWaveRenderer.endWidth = 0.02f;
        RoarWaveRenderer.useWorldSpace = true;
        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader != null)
        {
            RoarWaveRenderer.material = new Material(lineShader);
        }
        RoarWaveRenderer.startColor = new Color(1f, 0.72f, 0.22f, 0.95f);
        RoarWaveRenderer.endColor = new Color(1f, 0.28f, 0.1f, 0.25f);
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

    private void UpdateBossVisuals()
    {
        UpdateMeleeSwingVisual();
        UpdateRoarWaveVisual();
    }

    private void UpdateMeleeSwingVisual()
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
        float sweepAngle = Mathf.Lerp(-60f, 60f, normalizedTime);
        Vector3 origin = MeleeOrigin != null ? MeleeOrigin.position : transform.position + Vector3.up * 1.2f;
        Vector3 direction = Quaternion.Euler(0f, sweepAngle, 0f) * transform.forward;
        Vector3 endPoint = origin + direction * MeleeAttackRadius;

        MeleeSwingRenderer.SetPosition(0, origin);
        MeleeSwingRenderer.SetPosition(1, endPoint);
        float pulse = 0.5f + Mathf.Sin(Time.time * 24f) * 0.5f;
        MeleeSwingRenderer.startWidth = 0.16f + pulse * 0.06f;
        MeleeSwingRenderer.endWidth = 0.04f + pulse * 0.02f;
    }

    private void UpdateRoarWaveVisual()
    {
        if (RoarWaveRenderer == null)
        {
            EnsureLineRenderers();
        }

        if (RoarWaveRenderer == null)
        {
            return;
        }

        if (CurrentState != BossState.RoarAttack)
        {
            RoarWaveRenderer.enabled = false;
            return;
        }

        RoarWaveRenderer.enabled = true;
        Vector3 origin = EyeOrigin != null ? EyeOrigin.position : transform.position + Vector3.up * 1.6f;
        Vector3 target = origin + transform.forward * (3f + _roarTimer * 6f);
        RoarWaveRenderer.SetPosition(0, origin);
        RoarWaveRenderer.SetPosition(1, target);
        float pulse = 0.5f + Mathf.Sin(Time.time * 18f) * 0.5f;
        RoarWaveRenderer.startWidth = 0.18f + pulse * 0.08f;
        RoarWaveRenderer.endWidth = 0.06f + pulse * 0.05f;
    }

    private void ClearVortexField()
    {
        if (_activeVortexField != null)
        {
            Destroy(_activeVortexField.gameObject);
            _activeVortexField = null;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, MeleeAttackRange);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, VortexTriggerDistance);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 1f);
        Gizmos.DrawWireSphere(transform.position, RoarRange);
    }

    private GameObject CreateAnchorProjectile(Vector3 direction)
    {
        if (AnchorProjectilePrefab != null)
        {
            return Instantiate(AnchorProjectilePrefab, ProjectileOrigin.position, Quaternion.LookRotation(direction));
        }

        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        projectileObject.name = "HunterBossAnchorProjectile";
        projectileObject.transform.position = ProjectileOrigin.position;
        projectileObject.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
        projectileObject.transform.localScale = new Vector3(0.25f, 0.35f, 0.25f);

        Renderer rendererComponent = projectileObject.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.material.color = new Color(0.22f, 0.24f, 0.3f, 1f);
        }

        Collider projectileCollider = projectileObject.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }

        Rigidbody projectileRigidbody = projectileObject.GetComponent<Rigidbody>();
        if (projectileRigidbody == null)
        {
            projectileRigidbody = projectileObject.AddComponent<Rigidbody>();
        }

        HunterBossAnchorProjectile projectile = projectileObject.GetComponent<HunterBossAnchorProjectile>();
        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<HunterBossAnchorProjectile>();
        }

        return projectileObject;
    }
}
