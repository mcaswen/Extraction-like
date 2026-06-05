using Gameplay.SkillEffect;
using UnityEngine;

/// <summary>
/// 追猎者白模 Boss 行为。
/// 近战挥锚、漩涡控场后抛锚、低血怒吼音波。
/// </summary>
public class HunterBossBehaviorController : MonoBehaviour
{
    /// <summary>
    /// 追猎者 Boss 的主行为状态。
    /// </summary>
    public enum BossState
    {
        Idle,
        Chase,
        MeleeAttack,
        VortexAttack,
        RoarAttack,
        Cooldown
    }

    /// <summary>
    /// 当前 Boss 主行为状态。
    /// </summary>
    public BossState CurrentState;

    [Header("Config")]
    [SerializeField, Tooltip("Runtime source of truth for this boss's tunable values.")]
    private HunterBossConfig _config;

    [Header("References")]
    /// <summary>
    /// 当前玩家目标。
    /// </summary>
    public Transform PlayerTransform;

    /// <summary>
    /// 近战挥锚范围检测中心。
    /// </summary>
    public Transform MeleeOrigin;

    /// <summary>
    /// 船锚投射物生成点。
    /// </summary>
    public Transform ProjectileOrigin;

    /// <summary>
    /// 怒吼和掩体检测的起点。
    /// </summary>
    public Transform EyeOrigin;

    /// <summary>
    /// 近战挥锚的线渲染器。
    /// </summary>
    public LineRenderer MeleeSwingRenderer;

    /// <summary>
    /// 怒吼声波的线渲染器。
    /// </summary>
    public LineRenderer RoarWaveRenderer;

    [HideInInspector]
    public GameObject AnchorProjectilePrefab;
    [HideInInspector]
    public GameObject VortexFieldPrefab;

    [HideInInspector]
    public float DetectionRange = 18f;
    [HideInInspector]
    public float LoseRange = 24f;
    [HideInInspector]
    public float ChaseSpeed = 3.5f;

    [HideInInspector]
    public float MeleeAttackRange = 4f;
    [HideInInspector]
    public float MeleeAttackInterval = 2.4f;
    [HideInInspector]
    public float MeleeAttackRadius = 2.2f;
    [HideInInspector]
    public float MeleeDamage = 18f;
    [HideInInspector]
    public float MeleeKnockbackStrength = 6f;
    [HideInInspector]
    public float MeleeVisualDuration = 0.22f;

    [HideInInspector]
    public float VortexTriggerDistance = 10f;
    [HideInInspector]
    public int VortexTriggerMeleeCount = 3;
    [HideInInspector]
    public float VortexRadius = 3f;
    [HideInInspector]
    public float VortexChargeDuration = 2f;
    [HideInInspector]
    public float VortexImmobilizeDuration = 3f;
    [HideInInspector]
    public float AnchorThrowSpeed = 14f;
    [HideInInspector]
    public float AnchorThrowDamage = 16f;
    [HideInInspector]
    public float AnchorThrowKnockback = 5.5f;
    [HideInInspector]
    public float AnchorProjectileLifeTime = 4f;
    [HideInInspector]
    public float AnchorThrowCooldown = 3f;

    [HideInInspector]
    public float RageThreshold = 0.5f;
    [HideInInspector]
    public float RoarChargeDuration = 3f;
    [HideInInspector]
    public float RoarCooldown = 7f;
    [HideInInspector]
    public float RoarRange = 12f;
    [HideInInspector]
    public float RoarAngle = 120f;
    [HideInInspector]
    public float RoarDamage = 999f;
    [HideInInspector]
    public LayerMask CoverMask;
    [HideInInspector]
    public float CoverCheckHeight = 1.1f;
    [HideInInspector]
    public float RageShieldMaxHealthRatio = 0.2f;
    [HideInInspector]
    public float ForceFieldDefenseMultiplier = 2f;
    [HideInInspector]
    public float ForceFieldFrozenMoveSpeedMultiplier = 0.55f;
    [HideInInspector]
    public float ForceFieldFireDamageMultiplier = 3f;
    [HideInInspector]
    public float TrembleMoveSpeedMultiplier = 0.8f;
    [HideInInspector]
    public float TrembleAttackMultiplier = 0.8f;
    [HideInInspector]
    public float TrembleDuration = 3f;

    private EnemyHealthController _healthController;
    private PlayerHealthController _playerHealthController;
    private PlayerMovementController _playerMovementController;
    private float _meleeTimer;
    private float _vortexTimer;
    private float _cooldownTimer;
    private float _currentCooldownDuration;
    private float _roarTimer;
    private float _meleeVisualTimer;
    private float _vortexAnchorThrowTimer;
    private int _meleeHitCounter;
    private bool _hasTriggeredRageRoar;
    private bool _isForceFieldActive;
    private bool _isForceFieldFrozen;
    private HunterBossVortexField _activeVortexField;
    private EnemyStatusEffectController _statusEffectController;
    private Renderer[] _cachedRenderers;
    private Color[] _originalRendererColors;

    /// <summary>
    /// Boss 怒吼后的防御力场是否处于激活状态。
    /// </summary>
    public bool IsForceFieldActive => _isForceFieldActive;

    /// <summary>
    /// 防御力场是否被冰系效果冻结。
    /// </summary>
    public bool IsForceFieldFrozen => _isForceFieldFrozen;

    private void Start()
    {
        if (!ApplyConfig())
        {
            return;
        }

        _healthController = GetComponent<EnemyHealthController>();
        if (_healthController != null)
        {
            _healthController.ApplyConfig(_config);
        }
        _statusEffectController = GetComponent<EnemyStatusEffectController>();
        if (_statusEffectController == null)
        {
            _statusEffectController = gameObject.AddComponent<EnemyStatusEffectController>();
        }
        EnsurePlayerReferences();
        CacheRendererColors();

        EnsureLineRenderers();
        CurrentState = BossState.Idle;
    }

    private bool ApplyConfig()
    {
        if (_config == null)
        {
            Debug.LogError($"[{name}] Missing HunterBossConfig.", this);
            enabled = false;
            return false;
        }

        DetectionRange = _config.DetectionRange;
        LoseRange = _config.LoseRange;
        ChaseSpeed = _config.ChaseSpeed;
        MeleeAttackRange = _config.MeleeAttackRange;
        MeleeAttackInterval = _config.MeleeAttackInterval;
        MeleeAttackRadius = _config.MeleeAttackRadius;
        MeleeDamage = _config.MeleeDamage;
        MeleeKnockbackStrength = _config.MeleeKnockbackStrength;
        MeleeVisualDuration = _config.MeleeVisualDuration;
        AnchorProjectilePrefab = _config.AnchorProjectilePrefab;
        VortexFieldPrefab = _config.VortexFieldPrefab;
        VortexTriggerDistance = _config.VortexTriggerDistance;
        VortexTriggerMeleeCount = _config.VortexTriggerMeleeCount;
        VortexRadius = _config.VortexRadius;
        VortexChargeDuration = _config.VortexChargeDuration;
        VortexImmobilizeDuration = _config.VortexImmobilizeDuration;
        AnchorThrowSpeed = _config.AnchorThrowSpeed;
        AnchorThrowDamage = _config.AnchorThrowDamage;
        AnchorThrowKnockback = _config.AnchorThrowKnockback;
        AnchorProjectileLifeTime = _config.AnchorProjectileLifeTime;
        AnchorThrowCooldown = _config.AnchorThrowCooldown;
        RageThreshold = _config.RageThreshold;
        RoarChargeDuration = _config.RoarChargeDuration;
        RoarCooldown = _config.RoarCooldown;
        RoarRange = _config.RoarRange;
        RoarAngle = _config.RoarAngle;
        RoarDamage = _config.RoarDamage;
        CoverMask = _config.CoverMask;
        CoverCheckHeight = _config.CoverCheckHeight;
        RageShieldMaxHealthRatio = _config.RageShieldMaxHealthRatio;
        ForceFieldDefenseMultiplier = _config.ForceFieldDefenseMultiplier;
        ForceFieldFrozenMoveSpeedMultiplier = _config.ForceFieldFrozenMoveSpeedMultiplier;
        ForceFieldFireDamageMultiplier = _config.ForceFieldFireDamageMultiplier;
        TrembleMoveSpeedMultiplier = _config.TrembleMoveSpeedMultiplier;
        TrembleAttackMultiplier = _config.TrembleAttackMultiplier;
        TrembleDuration = _config.TrembleDuration;
        return true;
    }

    private void Update()
    {
        if (!EnsurePlayerReferences() || _playerHealthController == null)
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, PlayerTransform.position);
        HandleRageRoar(distanceToPlayer);
        TickForceFieldElementalState();

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
            bool didHitPlayer = PerformMeleeAttack();
            if (didHitPlayer)
            {
                _meleeHitCounter++;
            }

            if (_meleeHitCounter >= VortexTriggerMeleeCount && distanceToPlayer <= VortexTriggerDistance)
            {
                // 近战命中累计到阈值后切入漩涡 + 抛锚连招。
                _meleeHitCounter = 0;
                CurrentState = BossState.VortexAttack;
                _vortexTimer = 0f;
                _vortexAnchorThrowTimer = 0f;
                SpawnOrMoveVortexField();
                return;
            }

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

        if (_activeVortexField != null && _vortexTimer >= VortexChargeDuration)
        {
            // 蓄力完成后漩涡才真正定身玩家，给玩家留出离开范围的窗口。
            _activeVortexField.Arm();
        }

        if (_vortexTimer >= VortexChargeDuration)
        {
            _vortexAnchorThrowTimer += Time.deltaTime;
            if (_vortexAnchorThrowTimer >= VortexImmobilizeDuration)
            {
                // 漩涡控制结束后抛出船锚，再清理场地效果并进入冷却。
                _meleeHitCounter = 0;
                ThrowAnchorProjectile();
                ClearVortexField();
                EnterCooldown(AnchorThrowCooldown);
            }
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

            if (_meleeHitCounter >= VortexTriggerMeleeCount && distanceToPlayer <= VortexTriggerDistance)
            {
                CurrentState = BossState.VortexAttack;
                _vortexTimer = 0f;
                _vortexAnchorThrowTimer = 0f;
                SpawnOrMoveVortexField();
                return;
            }

            CurrentState = BossState.Chase;
        }
    }

    private bool PerformMeleeAttack()
    {
        // 追猎者近战只检测玩家，命中后累计漩涡触发次数。
        bool didHitPlayer = false;
        _meleeVisualTimer = MeleeVisualDuration;
        float totalDamage = 0f;
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
                totalDamage += playerHealth.TakeDamage(MeleeDamage);
                didHitPlayer = true;
            }

            if (playerMovement != null)
            {
                Vector3 pushDirection = hit.transform.position - transform.position;
                playerMovement.ApplyExternalImpulse(pushDirection, MeleeKnockbackStrength);
            }
        }

        EnemySkillDamageLogger.LogSkillDamage(this, "Anchor Sweep", totalDamage);
        return didHitPlayer;
    }

    private void SpawnOrMoveVortexField()
    {
        // 漩涡以 Boss 位置为中心，已有实例时复用并移动，避免重复生成多个控制区。
        Vector3 vortexPosition = transform.position;
        vortexPosition.y = 0.02f;

        if (_activeVortexField == null)
        {
            if (VortexFieldPrefab != null)
            {
                GameObject vortexObject = Instantiate(VortexFieldPrefab, vortexPosition, Quaternion.identity);
                SkillEffectLayerUtility.ApplyToRoot(vortexObject);
                _activeVortexField = vortexObject.GetComponent<HunterBossVortexField>();
            }

            if (_activeVortexField == null)
            {
                GameObject vortexObject = new GameObject("HunterBossVortexField");
                vortexObject.transform.position = vortexPosition;
                SkillEffectLayerUtility.ApplyToRoot(vortexObject);
                _activeVortexField = vortexObject.AddComponent<HunterBossVortexField>();
            }

            _activeVortexField.Configure(VortexRadius, VortexChargeDuration + VortexImmobilizeDuration + 0.2f, VortexImmobilizeDuration, false);
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
            projectile.LifeTime = AnchorProjectileLifeTime;
            projectile.Damage = AnchorThrowDamage;
            projectile.KnockbackStrength = AnchorThrowKnockback;
            projectile.SourceEnemy = gameObject;
            projectile.SkillName = "Anchor Throw";
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
        _isForceFieldActive = true;
        _isForceFieldFrozen = false;
        // 怒吼触发时补护盾并降低实际受伤倍率，形成一次低血防御阶段。
        if (_healthController != null)
        {
            _healthController.AddShield(_healthController.MaxHealth * RageShieldMaxHealthRatio);
            _healthController.SetDamageTakenMultiplier(1f / Mathf.Max(1f, ForceFieldDefenseMultiplier));
        }

        CurrentState = BossState.RoarAttack;
        _roarTimer = 0f;
        _vortexAnchorThrowTimer = 0f;
        _meleeHitCounter = 0;
        ClearVortexField();
    }

    private void ExecuteRoar(float distanceToPlayer)
    {
        float totalDamage = 0f;
        if (_playerHealthController == null || distanceToPlayer > RoarRange || !IsPlayerInRoarCone())
        {
            EnemySkillDamageLogger.LogSkillDamage(this, "Rage Roar", totalDamage);
            return;
        }

        if (IsPlayerProtectedByCover())
        {
            // 掩体不会完全免疫怒吼，但会把伤害压到很低，并仍然施加震慑。
            totalDamage = _playerHealthController.TakeDamage(RoarDamage * 0.1f);
            ApplyTrembleToPlayer();
            EnemySkillDamageLogger.LogSkillDamage(this, "Rage Roar", totalDamage);
            return;
        }

        totalDamage = _playerHealthController.TakeDamage(RoarDamage);
        ApplyTrembleToPlayer();
        EnemySkillDamageLogger.LogSkillDamage(this, "Rage Roar", totalDamage);
    }

    private void ApplyTrembleToPlayer()
    {
        if (TrembleDuration <= 0f)
        {
            return;
        }

        _playerMovementController?.ApplyMoveSpeedDebuff(TrembleMoveSpeedMultiplier, TrembleDuration);
        PlayerShootingController playerShootingController = PlayerTransform != null
            ? PlayerTransform.GetComponent<PlayerShootingController>()
            : PlayerShootingController.Instance;
        playerShootingController?.ApplyAttackMultiplierDebuff(TrembleAttackMultiplier, TrembleDuration);
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

    private bool IsPlayerInRoarCone()
    {
        if (PlayerTransform == null)
        {
            return false;
        }

        Vector3 directionToPlayer = PlayerTransform.position - transform.position;
        directionToPlayer.y = 0f;
        if (directionToPlayer.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        return Vector3.Angle(transform.forward, directionToPlayer.normalized) <= RoarAngle * 0.5f;
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

        float speedMultiplier = _isForceFieldFrozen ? ForceFieldFrozenMoveSpeedMultiplier : 1f;
        transform.position += direction.normalized * ChaseSpeed * speedMultiplier * Time.deltaTime;
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
        SkillEffectLayerUtility.ApplyToRoot(lineObject);
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
        SkillEffectLayerUtility.ApplyToRoot(lineObject);

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
        UpdateForceFieldVisual();
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

    private void TickForceFieldElementalState()
    {
        if (!_isForceFieldActive || _statusEffectController == null)
        {
            return;
        }

        if (_statusEffectController.IsFrozen || _statusEffectController.SlowMultiplier < 1f)
        {
            // 冰系状态不直接冻结 Boss，而是冻结力场并让移动按专用倍率减速。
            _isForceFieldFrozen = true;
        }

        if (_isForceFieldFrozen && _statusEffectController.IsFrozen)
        {
            _statusEffectController.BreakFreeze();
            _statusEffectController.ApplySlow(ForceFieldFrozenMoveSpeedMultiplier, 0.2f);
        }
    }

    /// <summary>
    /// 火系攻击打破已冻结力场时调用，清除力场冻结和减速。
    /// </summary>
    public void NotifyFrozenForceFieldBrokenByFire()
    {
        if (!_isForceFieldActive)
        {
            return;
        }

        _isForceFieldFrozen = false;
        _statusEffectController?.BreakSlow();
    }

    private void CacheRendererColors()
    {
        _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        _originalRendererColors = new Color[_cachedRenderers.Length];

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            _originalRendererColors[i] = rendererComponent != null && rendererComponent.material.HasProperty("_Color")
                ? rendererComponent.material.color
                : Color.white;
        }
    }

    private void UpdateForceFieldVisual()
    {
        if (!_isForceFieldActive || _cachedRenderers == null || _originalRendererColors == null)
        {
            return;
        }

        Color tintColor = _isForceFieldFrozen
            ? new Color(0.42f, 0.74f, 1f, 1f)
            : new Color(0.72f, 0.56f, 1f, 1f);
        float pulse = 0.5f + Mathf.Sin(Time.time * (_isForceFieldFrozen ? 5f : 9f)) * 0.5f;
        float tintStrength = (_isForceFieldFrozen ? 0.55f : 0.28f) * (0.65f + pulse * 0.35f);

        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            Renderer rendererComponent = _cachedRenderers[i];
            if (rendererComponent == null || !rendererComponent.material.HasProperty("_Color"))
            {
                continue;
            }

            rendererComponent.material.color = Color.Lerp(_originalRendererColors[i], tintColor, tintStrength);
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
            GameObject prefabProjectileObject = Instantiate(AnchorProjectilePrefab, ProjectileOrigin.position, Quaternion.LookRotation(direction));
            SkillEffectLayerUtility.ApplyToRoot(prefabProjectileObject);
            return prefabProjectileObject;
        }

        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        projectileObject.name = "HunterBossAnchorProjectile";
        projectileObject.transform.position = ProjectileOrigin.position;
        projectileObject.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
        projectileObject.transform.localScale = new Vector3(0.25f, 0.35f, 0.25f);
        SkillEffectLayerUtility.ApplyToRoot(projectileObject);

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
}
