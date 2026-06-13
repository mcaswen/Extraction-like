using System.Collections;
using System.Collections.Generic;
using Gameplay.Agent.Core;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class PlayerElementalSkillVfx : MonoBehaviour
{
    private enum ElementSlot
    {
        Ice,
        Earth,
        Fire,
        Metal,
        Water
    }

    private enum SkillSlot
    {
        Primary = 0,
        Secondary = 1
    }

    [Header("Input")]
    [SerializeField] private bool _enableKeyboardPreview = true;
    [SerializeField] private KeyCode _primarySkillKey = KeyCode.R;
    [SerializeField] private KeyCode _secondarySkillKey = KeyCode.T;
    [SerializeField] private KeyCode _cycleElementKey = KeyCode.C;
    [SerializeField] private KeyCode _iceElementKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode _earthElementKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode _fireElementKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode _metalElementKey = KeyCode.Alpha4;
    [SerializeField] private KeyCode _waterElementKey = KeyCode.Alpha5;
    [SerializeField] private ElementSlot _selectedElement = ElementSlot.Ice;

    [Header("Stats")]
    [SerializeField] private float _attackPower = 30f;
    [SerializeField] private float _defense = 28f;
    [SerializeField] private float _fallbackMaxHealth = 220f;

    [Header("Aim")]
    [SerializeField] private Camera _aimCamera;
    [SerializeField] private LayerMask _enemyMask = ~0;
    [SerializeField] private LayerMask _impactMask = ~0;
    [SerializeField] private float _aimGroundHeightOffset = 0.04f;
    [SerializeField] private float _maxAimDistance = 40f;
    [SerializeField] private Vector3 _castOriginOffset = new Vector3(0.25f, 0.9f, 0.45f);

    [Header("Ice")]
    [SerializeField] private float _iceConeCooldown = 9f;
    [SerializeField] private float _iceConeDamageMultiplier = 2.5f;
    [SerializeField] private float _iceConeAngle = 120f;
    [SerializeField] private float _iceConeRadius = 7.5f;
    [SerializeField] private float _iceSlowMultiplier = 0.5f;
    [SerializeField] private float _iceSlowDuration = 3f;
    [SerializeField] private float _winterCooldown = 12f;
    [SerializeField] private float _winterDamageMultiplier = 3f;
    [SerializeField] private float _winterRadius = 2.8f;

    [Header("Earth")]
    [SerializeField] private float _earthWallCooldown = 12f;
    [SerializeField] private float _earthWallDamageMultiplier = 0.8f;
    [SerializeField] private float _earthWallLength = 4.2f;
    [SerializeField] private float _earthWallWidth = 0.55f;
    [SerializeField] private float _earthWallHeight = 2.2f;
    [SerializeField] private float _earthWallDuration = 10f;
    [SerializeField] private float _earthQuakeCooldown = 15f;
    [SerializeField] private float _earthQuakeDamageMultiplier = 0.5f;
    [SerializeField] private float _earthQuakeRadius = 3.25f;
    [SerializeField] private float _earthQuakeDuration = 5f;

    [Header("Fire")]
    [SerializeField] private float _fireShieldCooldown = 18f;
    [SerializeField] private float _fireShieldAbsorbMultiplier = 3f;
    [SerializeField] private float _fireShieldBaseDuration = 5f;
    [SerializeField] private float _fireShieldMaxDuration = 10f;
    [SerializeField] private float _fireShieldDamageMultiplier = 0.5f;
    [SerializeField] private float _fireShieldDamageRadius = 2.75f;
    [SerializeField] private float _fireShieldTickInterval = 0.75f;
    [SerializeField] private float _fireballCooldown = 3f;
    [SerializeField] private float _fireballDamageMultiplier = 1.5f;
    [SerializeField] private float _fireballRadius = 0.38f;
    [SerializeField] private float _fireballSpeed = 14f;
    [SerializeField] private float _fireballLifetime = 2.2f;
    [SerializeField] private float _fireShieldExtendOnHit = 2f;

    [Header("Metal")]
    [SerializeField] private float _metalBuffCooldown = 14f;
    [SerializeField] private float _metalAttackBonusMultiplier = 1.5f;
    [SerializeField] private float _metalBuffDuration = 5f;
    [SerializeField] private float _smeltCooldown = 20f;
    [SerializeField] private float _smeltVulnerabilityMultiplier = 1.5f;
    [SerializeField] private float _smeltDuration = 10f;
    [SerializeField] private float _smeltRange = 18f;

    [Header("Water")]
    [SerializeField] private float _confluenceCooldown = 12f;
    [SerializeField] private float _confluenceRadius = 5f;
    [SerializeField] private float _confluenceDuration = 2f;
    [SerializeField] private float _confluenceCooldownReductionSeconds = 2f;
    [SerializeField] private float _confluenceMoveSpeedMultiplier = 1.45f;
    [SerializeField] private float _springCooldown = 9f;
    [SerializeField] private float _springHealMaxHealthRatio = 0.05f;
    [SerializeField] private float _springDuration = 3f;
    [SerializeField] private float _springRange = 12f;

    [Header("Visual")]
    [SerializeField, Range(16, 128)] private int _ringSegments = 72;
    [SerializeField, Range(6, 64)] private int _lineSegments = 24;
    [SerializeField] private float _groundOffset = 0.06f;

    private readonly Collider[] _overlapHits = new Collider[96];
    private readonly RaycastHit[] _sphereCastHits = new RaycastHit[32];
    private readonly HashSet<EnemyHealthController> _targets = new HashSet<EnemyHealthController>();
    private readonly Dictionary<EnemyHealthController, float> _vulnerableTargets = new Dictionary<EnemyHealthController, float>();

    private readonly float[,] _cooldowns = new float[5, 2];
    private Camera _cachedCamera;
    private AgentHealthController _playerHealth;
    private PlayerMovementController _playerMovement;
    private PlayerShootingController _playerShooting;
    private CharacterMotor _characterMotor;
    private Material _softMaterial;
    private Material _iceMaterial;
    private Material _earthMaterial;
    private Material _fireMaterial;
    private Material _metalMaterial;
    private Material _waterMaterial;
    private Material _darkLineMaterial;
    private GameObject _fireShieldVisual;
    private GameObject _metalBuffVisual;
    private float _fireShieldRemaining;
    private float _fireShieldTickTimer;
    private float _metalBuffRemaining;
    private float _confluenceRemaining;
    private float _logCooldown;

    private void Awake()
    {
        _cachedCamera = Camera.main;
        CachePlayerComponents();
    }

    private void OnDisable()
    {
        DestroyRuntimeMaterial(ref _softMaterial);
        DestroyRuntimeMaterial(ref _iceMaterial);
        DestroyRuntimeMaterial(ref _earthMaterial);
        DestroyRuntimeMaterial(ref _fireMaterial);
        DestroyRuntimeMaterial(ref _metalMaterial);
        DestroyRuntimeMaterial(ref _waterMaterial);
        DestroyRuntimeMaterial(ref _darkLineMaterial);
    }

    private void Update()
    {
        CachePlayerComponents();
        TickCooldowns();
        TickActiveEffects();
        TickVulnerableTargets();

        if (!_enableKeyboardPreview || IsInputBlocked())
        {
            return;
        }

        HandleElementSelection();

        if (Input.GetKeyDown(_primarySkillKey))
        {
            TryCast(_selectedElement, SkillSlot.Primary);
        }

        if (Input.GetKeyDown(_secondarySkillKey))
        {
            TryCast(_selectedElement, SkillSlot.Secondary);
        }
    }

    public void CastIceFrostAssault()
    {
        TryCast(ElementSlot.Ice, SkillSlot.Primary);
    }

    public void CastIceWinter()
    {
        TryCast(ElementSlot.Ice, SkillSlot.Secondary);
    }

    public void CastEarthRockWall()
    {
        TryCast(ElementSlot.Earth, SkillSlot.Primary);
    }

    public void CastEarthQuake()
    {
        TryCast(ElementSlot.Earth, SkillSlot.Secondary);
    }

    public void CastFireShield()
    {
        TryCast(ElementSlot.Fire, SkillSlot.Primary);
    }

    public void CastFireball()
    {
        TryCast(ElementSlot.Fire, SkillSlot.Secondary);
    }

    public void CastMetalBuff()
    {
        TryCast(ElementSlot.Metal, SkillSlot.Primary);
    }

    public void CastMetalSmelt()
    {
        TryCast(ElementSlot.Metal, SkillSlot.Secondary);
    }

    public void CastWaterConfluence()
    {
        TryCast(ElementSlot.Water, SkillSlot.Primary);
    }

    public void CastWaterSpring()
    {
        TryCast(ElementSlot.Water, SkillSlot.Secondary);
    }

    private void HandleElementSelection()
    {
        if (Input.GetKeyDown(_iceElementKey))
        {
            _selectedElement = ElementSlot.Ice;
        }
        else if (Input.GetKeyDown(_earthElementKey))
        {
            _selectedElement = ElementSlot.Earth;
        }
        else if (Input.GetKeyDown(_fireElementKey))
        {
            _selectedElement = ElementSlot.Fire;
        }
        else if (Input.GetKeyDown(_metalElementKey))
        {
            _selectedElement = ElementSlot.Metal;
        }
        else if (Input.GetKeyDown(_waterElementKey))
        {
            _selectedElement = ElementSlot.Water;
        }
        else if (Input.GetKeyDown(_cycleElementKey))
        {
            int next = ((int)_selectedElement + 1) % 5;
            _selectedElement = (ElementSlot)next;
        }
    }

    private bool TryCast(ElementSlot element, SkillSlot skill)
    {
        int elementIndex = (int)element;
        int skillIndex = (int)skill;
        if (_cooldowns[elementIndex, skillIndex] > 0f)
        {
            LogPreview($"Player skill {element}/{skill} cooling down: {_cooldowns[elementIndex, skillIndex]:0.0}s");
            return false;
        }

        bool didCast = false;
        float cooldown = 0f;
        switch (element)
        {
            case ElementSlot.Ice:
                didCast = skill == SkillSlot.Primary ? CastIceCone() : CastWinter();
                cooldown = skill == SkillSlot.Primary ? _iceConeCooldown : _winterCooldown;
                break;
            case ElementSlot.Earth:
                didCast = skill == SkillSlot.Primary ? CastEarthWall() : CastEarthQuakeInternal();
                cooldown = skill == SkillSlot.Primary ? _earthWallCooldown : _earthQuakeCooldown;
                break;
            case ElementSlot.Fire:
                didCast = skill == SkillSlot.Primary ? CastFireShieldInternal() : CastFireballInternal();
                cooldown = skill == SkillSlot.Primary ? _fireShieldCooldown : _fireballCooldown;
                break;
            case ElementSlot.Metal:
                didCast = skill == SkillSlot.Primary ? CastMetalBuffInternal() : CastSmelt();
                cooldown = skill == SkillSlot.Primary ? _metalBuffCooldown : _smeltCooldown;
                break;
            case ElementSlot.Water:
                didCast = skill == SkillSlot.Primary ? CastConfluence() : CastSpring();
                cooldown = skill == SkillSlot.Primary ? _confluenceCooldown : _springCooldown;
                break;
        }

        if (didCast)
        {
            _cooldowns[elementIndex, skillIndex] = cooldown;
        }

        return didCast;
    }

    private bool CastIceCone()
    {
        Vector3 origin = ResolveCastOrigin();
        Vector3 forward = ResolveAimDirection(origin);
        FaceDirection(forward);

        SpawnConeTelegraph(origin, forward, _iceConeRadius, _iceConeAngle, GetIceMaterial(), GetSoftMaterial(), 0.55f);
        SpawnDirectionalParticles("FrostAssaultMist", origin + Vector3.up * 0.2f, forward, GetIceMaterial(), 0.45f, 72, _iceConeRadius);

        CollectEnemiesInCone(origin, forward, _iceConeRadius, _iceConeAngle, _targets);
        foreach (EnemyHealthController enemy in _targets)
        {
            ApplyEnemyDamage(enemy, AttackDamage(_iceConeDamageMultiplier), enemy.transform.position, origin);
            GetOrCreateEnemyStatus(enemy).ApplySlow(_iceSlowMultiplier, _iceSlowDuration);
            SpawnSmallBurst(enemy.transform.position + Vector3.up * 0.75f, Color.white, new Color(0.45f, 0.9f, 1f, 0.65f), 0.7f);
        }

        int spikeCount = Mathf.Max(7, Mathf.RoundToInt(_iceConeAngle / 12f));
        for (int i = 0; i < spikeCount; i++)
        {
            float t = spikeCount == 1 ? 0.5f : i / (float)(spikeCount - 1);
            float angle = Mathf.Lerp(-_iceConeAngle * 0.5f, _iceConeAngle * 0.5f, t);
            float distance = Mathf.Lerp(1.25f, _iceConeRadius * 0.95f, Mathf.PingPong(i * 0.37f, 1f));
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            SpawnIceSpike(origin + direction * distance, direction, 0.45f + t * 0.25f);
        }

        return true;
    }

    private bool CastWinter()
    {
        Vector3 target = ResolveAimPoint(_winterRadius + 4f);
        target.y += _groundOffset;
        SpawnGroundRing("WinterTargetRing", target, _winterRadius, GetIceMaterial(), 0.13f, 0.75f);
        StartCoroutine(WinterRoutine(target));
        return true;
    }

    private IEnumerator WinterRoutine(Vector3 target)
    {
        SpawnSmallBurst(target + Vector3.up * 2.9f, new Color(0.74f, 0.96f, 1f, 0.8f), Color.white, 1f);
        yield return new WaitForSeconds(0.22f);

        CollectEnemiesInSphere(target, _winterRadius, _targets);
        foreach (EnemyHealthController enemy in _targets)
        {
            ApplyEnemyDamage(enemy, AttackDamage(_winterDamageMultiplier), enemy.transform.position, target);
        }

        int pillarCount = 9;
        for (int i = 0; i < pillarCount; i++)
        {
            float angle = i * (360f / pillarCount);
            float radius = i == 0 ? 0f : Random.Range(_winterRadius * 0.2f, _winterRadius * 0.78f);
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
            SpawnIcePillar(target + offset, Random.Range(1.35f, 2.65f), Random.Range(0.16f, 0.34f), Random.Range(0.9f, 1.25f));
        }
    }

    private bool CastEarthWall()
    {
        Vector3 forward = FlattenedForward();
        Vector3 center = transform.position + forward * 2.15f + Vector3.up * (_earthWallHeight * 0.5f);
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        GameObject wall = new GameObject("PlayerEarthWall");
        wall.layer = gameObject.layer;
        wall.transform.SetPositionAndRotation(center, rotation);

        int blockCount = 7;
        for (int i = 0; i < blockCount; i++)
        {
            float t = blockCount == 1 ? 0.5f : i / (float)(blockCount - 1);
            float x = Mathf.Lerp(-_earthWallLength * 0.5f, _earthWallLength * 0.5f, t);
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "EarthWallBlock";
            block.layer = gameObject.layer;
            block.transform.SetParent(wall.transform, false);
            block.transform.localPosition = new Vector3(x, Random.Range(-0.08f, 0.1f), Random.Range(-0.04f, 0.04f));
            block.transform.localRotation = Quaternion.Euler(0f, Random.Range(-6f, 6f), Random.Range(-4f, 4f));
            block.transform.localScale = new Vector3(
                _earthWallLength / blockCount * Random.Range(0.95f, 1.18f),
                _earthWallHeight * Random.Range(0.82f, 1.05f),
                _earthWallWidth * Random.Range(0.8f, 1.18f));
            ApplyRenderer(block, GetEarthMaterial());
        }

        Rigidbody wallBody = wall.AddComponent<Rigidbody>();
        wallBody.isKinematic = true;
        wallBody.useGravity = false;
        SpawnGroundRing("EarthWallDustRing", center - Vector3.up * (_earthWallHeight * 0.5f - _groundOffset), _earthWallLength * 0.55f, GetEarthMaterial(), 0.11f, 0.65f);
        SpawnSmallBurst(center, new Color(0.44f, 0.34f, 0.22f, 0.85f), new Color(0.9f, 0.72f, 0.45f, 0.4f), 1.1f);

        DamageEnemiesInWallBox(center, rotation, DefenseDamage(_earthWallDamageMultiplier));
        Destroy(wall, _earthWallDuration);
        return true;
    }

    private bool CastEarthQuakeInternal()
    {
        Vector3 target = ResolveAimPoint(_earthQuakeRadius + 4f);
        StartCoroutine(EarthQuakeRoutine(target));
        return true;
    }

    private IEnumerator EarthQuakeRoutine(Vector3 target)
    {
        float elapsed = 0f;
        float tick = 0f;
        GameObject root = new GameObject("PlayerEarthQuakeArray");
        root.layer = gameObject.layer;
        root.transform.position = target;

        LineRenderer outer = CreateLine(root.transform, "EarthQuakeOuter", GetEarthMaterial(), 0.12f, 8);
        LineRenderer inner = CreateLine(root.transform, "EarthQuakeInner", GetDarkLineMaterial(), 0.06f, 9);
        LineRenderer crackA = CreateLine(root.transform, "EarthCrackA", GetDarkLineMaterial(), 0.08f, 10);
        LineRenderer crackB = CreateLine(root.transform, "EarthCrackB", GetDarkLineMaterial(), 0.08f, 10);

        while (elapsed < _earthQuakeDuration)
        {
            elapsed += Time.deltaTime;
            tick -= Time.deltaTime;
            float pulse = 0.85f + Mathf.Sin(Time.time * 12f) * 0.15f;
            ApplyLine(outer, BuildCircle(target, _earthQuakeRadius * pulse, _groundOffset), new Color(0.72f, 0.55f, 0.28f, 0.9f), new Color(0.36f, 0.2f, 0.1f, 0.65f), 0.85f, 0.12f);
            ApplyLine(inner, BuildCircle(target, _earthQuakeRadius * 0.58f, _groundOffset + 0.01f), new Color(0.95f, 0.76f, 0.35f, 0.85f), new Color(0.22f, 0.13f, 0.08f, 0.7f), 0.8f, 0.06f);
            ApplyLine(crackA, BuildCrack(target, _earthQuakeRadius, Time.time * 25f), Color.black, new Color(0.85f, 0.55f, 0.2f, 0.7f), 0.75f, 0.08f);
            ApplyLine(crackB, BuildCrack(target, _earthQuakeRadius, Time.time * -18f + 90f), Color.black, new Color(0.85f, 0.55f, 0.2f, 0.7f), 0.75f, 0.08f);

            if (tick <= 0f)
            {
                tick = 1f;
                CollectEnemiesInSphere(target, _earthQuakeRadius, _targets);
                foreach (EnemyHealthController enemy in _targets)
                {
                    ApplyEnemyDamage(enemy, DefenseDamage(_earthQuakeDamageMultiplier), enemy.transform.position, target);
                }

                SpawnSmallBurst(target + Vector3.up * 0.12f, new Color(0.58f, 0.39f, 0.18f, 0.85f), new Color(0.95f, 0.65f, 0.25f, 0.45f), 0.9f);
            }

            yield return null;
        }

        Destroy(root);
    }

    private bool CastFireShieldInternal()
    {
        _fireShieldRemaining = Mathf.Min(_fireShieldMaxDuration, Mathf.Max(_fireShieldRemaining, _fireShieldBaseDuration));
        _fireShieldTickTimer = 0f;

        if (_playerHealth != null)
        {
            _playerHealth.AddShield(AttackDamage(_fireShieldAbsorbMultiplier), _fireShieldRemaining);
        }

        if (_fireShieldVisual == null)
        {
            _fireShieldVisual = CreateFollowAura("PlayerFireShieldAura", GetFireMaterial(), _fireShieldDamageRadius, 0.2f);
        }

        SpawnSmallBurst(transform.position + Vector3.up * 0.9f, new Color(1f, 0.5f, 0.16f, 0.9f), new Color(1f, 0.94f, 0.54f, 0.65f), 1.2f);
        return true;
    }

    private bool CastFireballInternal()
    {
        Vector3 origin = ResolveCastOrigin();
        Vector3 direction = ResolveAimDirection(origin);
        FaceDirection(direction);
        StartCoroutine(FireballRoutine(origin, direction));
        return true;
    }

    private IEnumerator FireballRoutine(Vector3 origin, Vector3 direction)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "PlayerFireball";
        projectile.layer = gameObject.layer;
        projectile.transform.position = origin;
        projectile.transform.localScale = Vector3.one * (_fireballRadius * 2f);
        ApplyRenderer(projectile, GetFireMaterial());
        RemoveCollider(projectile);

        ParticleSystem trail = projectile.AddComponent<ParticleSystem>();
        ConfigureTrailParticles(trail, new Color(1f, 0.45f, 0.08f, 0.9f), new Color(1f, 0.9f, 0.35f, 0.55f), 0.25f, 0.12f);

        float elapsed = 0f;
        Vector3 previous = origin;
        while (elapsed < _fireballLifetime)
        {
            elapsed += Time.deltaTime;
            Vector3 next = previous + direction * (_fireballSpeed * Time.deltaTime);
            if (TryHitEnemySphere(previous, direction, Vector3.Distance(previous, next) + _fireballRadius, _fireballRadius, out EnemyHealthController enemy, out Vector3 hitPoint))
            {
                ApplyEnemyDamage(enemy, AttackDamage(_fireballDamageMultiplier), hitPoint, origin);
                _fireShieldRemaining = Mathf.Min(_fireShieldMaxDuration, _fireShieldRemaining + _fireShieldExtendOnHit);
                SpawnSmallBurst(hitPoint, new Color(1f, 0.42f, 0.05f, 0.95f), new Color(1f, 0.94f, 0.45f, 0.8f), 1.2f);
                Destroy(projectile);
                yield break;
            }

            projectile.transform.position = next;
            previous = next;
            yield return null;
        }

        SpawnSmallBurst(projectile.transform.position, new Color(1f, 0.42f, 0.05f, 0.85f), new Color(1f, 0.94f, 0.45f, 0.55f), 0.9f);
        Destroy(projectile);
    }

    private bool CastMetalBuffInternal()
    {
        _metalBuffRemaining = Mathf.Max(_metalBuffRemaining, _metalBuffDuration);
        if (_metalBuffVisual == null)
        {
            _metalBuffVisual = CreateFollowAura("PlayerMetalBuffAura", GetMetalMaterial(), 1.65f, 0.1f);
        }

        SpawnSmallBurst(transform.position + Vector3.up * 0.8f, new Color(0.95f, 0.86f, 0.48f, 0.9f), new Color(1f, 1f, 0.85f, 0.7f), 1.1f);
        return true;
    }

    private bool CastSmelt()
    {
        if (!TryFindTargetedEnemy(_smeltRange, out EnemyHealthController enemy))
        {
            LogPreview("Player smelt found no enemy target.");
            return false;
        }

        _vulnerableTargets[enemy] = Time.time + _smeltDuration;
        StartCoroutine(SmeltMarkRoutine(enemy, _smeltDuration));
        SpawnSmallBurst(enemy.transform.position + Vector3.up * 1f, new Color(1f, 0.72f, 0.24f, 0.9f), new Color(1f, 0.96f, 0.7f, 0.7f), 0.8f);
        return true;
    }

    private IEnumerator SmeltMarkRoutine(EnemyHealthController enemy, float duration)
    {
        GameObject root = new GameObject("PlayerSmeltVulnerableMark");
        root.layer = gameObject.layer;
        LineRenderer ring = CreateLine(root.transform, "SmeltRing", GetMetalMaterial(), 0.08f, 10);
        LineRenderer spark = CreateLine(root.transform, "SmeltSpark", GetFireMaterial(), 0.05f, 11);
        float elapsed = 0f;

        while (elapsed < duration && enemy != null && enemy.IsAlive)
        {
            elapsed += Time.deltaTime;
            Vector3 center = enemy.transform.position + Vector3.up * 1.1f;
            float radius = 0.65f + Mathf.Sin(Time.time * 7f) * 0.08f;
            ApplyLine(ring, BuildVerticalCircle(center, radius, transform.forward), new Color(1f, 0.74f, 0.22f, 0.9f), new Color(1f, 0.96f, 0.64f, 0.75f), 0.9f, 0.08f);
            ApplyLine(spark, BuildVerticalSpark(center, radius, Time.time * 110f), new Color(1f, 0.4f, 0.08f, 0.9f), Color.white, 0.85f, 0.05f);
            yield return null;
        }

        Destroy(root);
    }

    private bool CastConfluence()
    {
        _confluenceRemaining = Mathf.Max(_confluenceRemaining, _confluenceDuration);
        ReduceAllCooldowns(_confluenceCooldownReductionSeconds);
        if (_playerMovement != null)
        {
            _playerMovement.ApplyMoveSpeedMultiplier(_confluenceMoveSpeedMultiplier, _confluenceDuration);
        }

        if (_characterMotor != null)
        {
            _characterMotor.ApplyMoveSpeedMultiplier(_confluenceMoveSpeedMultiplier, _confluenceDuration);
        }

        SpawnGroundRing("PlayerWaterConfluenceRing", transform.position + Vector3.up * _groundOffset, _confluenceRadius, GetWaterMaterial(), 0.14f, _confluenceDuration);
        SpawnSmallBurst(transform.position + Vector3.up * 0.8f, new Color(0.18f, 0.85f, 1f, 0.9f), new Color(0.82f, 1f, 1f, 0.72f), 1.1f);
        return true;
    }

    private bool CastSpring()
    {
        AgentHealthController target = ResolveSpringTarget();
        if (target == null)
        {
            LogPreview("Player spring found no ally target.");
            return false;
        }

        StartCoroutine(SpringRoutine(target));
        return true;
    }

    private IEnumerator SpringRoutine(AgentHealthController target)
    {
        float elapsed = 0f;
        float tick = 0f;
        GameObject root = new GameObject("PlayerWaterSpring");
        root.layer = gameObject.layer;
        LineRenderer ring = CreateLine(root.transform, "SpringRing", GetWaterMaterial(), 0.09f, 11);

        while (elapsed < _springDuration && target != null && !target.IsDead)
        {
            elapsed += Time.deltaTime;
            tick -= Time.deltaTime;
            Vector3 center = target.transform.position + Vector3.up * _groundOffset;
            root.transform.position = center;
            ApplyLine(ring, BuildCircle(center, 1.05f + Mathf.Sin(Time.time * 8f) * 0.12f, 0.02f), new Color(0.36f, 0.94f, 1f, 0.9f), Color.white, 0.9f, 0.09f);

            if (tick <= 0f)
            {
                tick = 1f;
                target.Heal(ResolveMaxHealth(target) * _springHealMaxHealthRatio);
                SpawnSmallBurst(target.transform.position + Vector3.up * 1f, new Color(0.32f, 0.9f, 1f, 0.85f), Color.white, 0.75f);
            }

            yield return null;
        }

        Destroy(root);
    }

    private void TickCooldowns()
    {
        float delta = Time.deltaTime;
        for (int element = 0; element < 5; element++)
        {
            for (int skill = 0; skill < 2; skill++)
            {
                _cooldowns[element, skill] = Mathf.Max(0f, _cooldowns[element, skill] - delta);
            }
        }

        _logCooldown = Mathf.Max(0f, _logCooldown - delta);
    }

    private void TickActiveEffects()
    {
        if (_fireShieldRemaining > 0f)
        {
            _fireShieldRemaining = Mathf.Max(0f, _fireShieldRemaining - Time.deltaTime);
            _fireShieldTickTimer -= Time.deltaTime;
            if (_fireShieldVisual != null)
            {
                _fireShieldVisual.transform.position = transform.position + Vector3.up * 0.85f;
            }

            if (_fireShieldTickTimer <= 0f)
            {
                _fireShieldTickTimer = _fireShieldTickInterval;
                CollectEnemiesInSphere(transform.position, _fireShieldDamageRadius, _targets);
                foreach (EnemyHealthController enemy in _targets)
                {
                    ApplyEnemyDamage(enemy, AttackDamage(_fireShieldDamageMultiplier), enemy.transform.position, transform.position);
                }
            }
        }
        else if (_fireShieldVisual != null)
        {
            Destroy(_fireShieldVisual);
            _fireShieldVisual = null;
        }

        if (_metalBuffRemaining > 0f)
        {
            _metalBuffRemaining = Mathf.Max(0f, _metalBuffRemaining - Time.deltaTime);
            if (_metalBuffVisual != null)
            {
                _metalBuffVisual.transform.position = transform.position + Vector3.up * 0.85f;
                _metalBuffVisual.transform.Rotate(Vector3.up, 80f * Time.deltaTime, Space.World);
            }
        }
        else if (_metalBuffVisual != null)
        {
            Destroy(_metalBuffVisual);
            _metalBuffVisual = null;
        }

        _confluenceRemaining = Mathf.Max(0f, _confluenceRemaining - Time.deltaTime);
    }

    private void TickVulnerableTargets()
    {
        if (_vulnerableTargets.Count == 0)
        {
            return;
        }

        s_expiredTargets.Clear();
        foreach (KeyValuePair<EnemyHealthController, float> pair in _vulnerableTargets)
        {
            if (pair.Key == null || !pair.Key.IsAlive || Time.time >= pair.Value)
            {
                s_expiredTargets.Add(pair.Key);
            }
        }

        for (int i = 0; i < s_expiredTargets.Count; i++)
        {
            _vulnerableTargets.Remove(s_expiredTargets[i]);
        }
    }

    private void ReduceAllCooldowns(float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        for (int element = 0; element < 5; element++)
        {
            for (int skill = 0; skill < 2; skill++)
            {
                _cooldowns[element, skill] = Mathf.Max(0f, _cooldowns[element, skill] - seconds);
            }
        }
    }

    private void CollectEnemiesInSphere(Vector3 center, float radius, HashSet<EnemyHealthController> results)
    {
        results.Clear();
        int count = Physics.OverlapSphereNonAlloc(center, radius, _overlapHits, _enemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            EnemyHealthController enemy = ResolveEnemy(_overlapHits[i]);
            if (enemy != null)
            {
                results.Add(enemy);
            }
        }
    }

    private void CollectEnemiesInCone(Vector3 origin, Vector3 forward, float radius, float angle, HashSet<EnemyHealthController> results)
    {
        CollectEnemiesInSphere(origin, radius, results);
        s_removedTargets.Clear();
        foreach (EnemyHealthController enemy in results)
        {
            Vector3 direction = enemy.transform.position - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f ||
                Vector3.Angle(forward, direction.normalized) > angle * 0.5f)
            {
                s_removedTargets.Add(enemy);
            }
        }

        for (int i = 0; i < s_removedTargets.Count; i++)
        {
            results.Remove(s_removedTargets[i]);
        }
    }

    private void DamageEnemiesInWallBox(Vector3 center, Quaternion rotation, float damage)
    {
        _targets.Clear();
        Vector3 halfExtents = new Vector3(_earthWallLength * 0.6f, _earthWallHeight * 0.55f, _earthWallWidth * 1.8f);
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapHits, rotation, _enemyMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            EnemyHealthController enemy = ResolveEnemy(_overlapHits[i]);
            if (enemy != null && _targets.Add(enemy))
            {
                ApplyEnemyDamage(enemy, damage, enemy.transform.position, center);
            }
        }
    }

    private bool TryHitEnemySphere(
        Vector3 origin,
        Vector3 direction,
        float distance,
        float radius,
        out EnemyHealthController enemy,
        out Vector3 hitPoint)
    {
        enemy = null;
        hitPoint = origin + direction * distance;
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, _sphereCastHits, distance, _enemyMask, QueryTriggerInteraction.Collide);
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            EnemyHealthController candidate = ResolveEnemy(_sphereCastHits[i].collider);
            if (candidate == null)
            {
                continue;
            }

            if (_sphereCastHits[i].distance < bestDistance)
            {
                bestDistance = _sphereCastHits[i].distance;
                enemy = candidate;
                hitPoint = _sphereCastHits[i].point;
            }
        }

        return enemy != null;
    }

    private bool TryFindTargetedEnemy(float range, out EnemyHealthController enemy)
    {
        Vector3 origin = ResolveCastOrigin();
        Vector3 direction = ResolveAimDirection(origin);
        if (TryHitEnemySphere(origin, direction, range, 0.45f, out enemy, out _))
        {
            return true;
        }

        CollectEnemiesInCone(origin, direction, range, 28f, _targets);
        float bestDistance = float.MaxValue;
        enemy = null;
        foreach (EnemyHealthController candidate in _targets)
        {
            float distance = Vector3.Distance(origin, candidate.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                enemy = candidate;
            }
        }

        return enemy != null;
    }

    private EnemyHealthController ResolveEnemy(Collider hit)
    {
        if (hit == null)
        {
            return null;
        }

        EnemyHealthController enemy = hit.GetComponentInParent<EnemyHealthController>();
        if (enemy == null)
        {
            enemy = hit.GetComponentInChildren<EnemyHealthController>();
        }

        return enemy != null && enemy.IsAlive ? enemy : null;
    }

    private EnemyStatusEffectController GetOrCreateEnemyStatus(EnemyHealthController enemy)
    {
        EnemyStatusEffectController status = enemy.GetComponent<EnemyStatusEffectController>();
        if (status == null)
        {
            status = enemy.gameObject.AddComponent<EnemyStatusEffectController>();
        }

        return status;
    }

    private void ApplyEnemyDamage(EnemyHealthController enemy, float damage, Vector3 hitPoint, Vector3 sourcePoint)
    {
        if (enemy == null || !enemy.IsAlive || damage <= 0f)
        {
            return;
        }

        if (_vulnerableTargets.TryGetValue(enemy, out float vulnerableUntil) && Time.time < vulnerableUntil)
        {
            damage *= _smeltVulnerabilityMultiplier;
        }

        if (_metalBuffRemaining > 0f)
        {
            damage *= _metalAttackBonusMultiplier;
        }

        Vector3 incomingDirection = hitPoint - sourcePoint;
        enemy.TakeDamage(
            damage,
            EnemyDamageContext.FromPlayer(transform, hitPoint, sourcePoint, incomingDirection, EnemyDamageSourceType.Magic));
    }

    private AgentHealthController ResolveSpringTarget()
    {
        Camera cameraToUse = ResolveCamera();
        if (cameraToUse != null)
        {
            Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
            int count = Physics.SphereCastNonAlloc(ray, 0.45f, _sphereCastHits, _springRange, _impactMask, QueryTriggerInteraction.Collide);
            float bestDistance = float.MaxValue;
            AgentHealthController best = null;
            for (int i = 0; i < count; i++)
            {
                AgentHealthController candidate = _sphereCastHits[i].collider.GetComponentInParent<AgentHealthController>();
                if (candidate == null || candidate.IsDead)
                {
                    continue;
                }

                if (_sphereCastHits[i].distance < bestDistance)
                {
                    bestDistance = _sphereCastHits[i].distance;
                    best = candidate;
                }
            }

            if (best != null)
            {
                return best;
            }
        }

        return _playerHealth;
    }

    private float AttackDamage(float multiplier)
    {
        return Mathf.Max(0f, _attackPower * multiplier);
    }

    private float DefenseDamage(float multiplier)
    {
        return Mathf.Max(0f, _defense * multiplier);
    }

    private float ResolveMaxHealth(AgentHealthController health)
    {
        if (health != null)
        {
            return Mathf.Max(1f, health.MaxHealth);
        }

        return Mathf.Max(1f, _fallbackMaxHealth);
    }

    private Vector3 ResolveCastOrigin()
    {
        return transform.TransformPoint(_castOriginOffset);
    }

    private Vector3 ResolveAimPoint(float fallbackDistance)
    {
        Vector3 origin = ResolveCastOrigin();
        Camera cameraToUse = ResolveCamera();
        if (cameraToUse != null)
        {
            Ray ray = cameraToUse.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.up, transform.position + Vector3.up * _aimGroundHeightOffset);
            if (plane.Raycast(ray, out float distance) && distance <= _maxAimDistance)
            {
                return ray.GetPoint(distance);
            }
        }

        return transform.position + FlattenedForward() * fallbackDistance;
    }

    private Vector3 ResolveAimDirection(Vector3 origin)
    {
        Vector3 aimPoint = ResolveAimPoint(_maxAimDistance);
        Vector3 direction = aimPoint - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        return FlattenedForward();
    }

    private Vector3 FlattenedForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        return forward.normalized;
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Camera ResolveCamera()
    {
        if (_aimCamera != null)
        {
            return _aimCamera;
        }

        if (_cachedCamera == null)
        {
            _cachedCamera = Camera.main;
        }

        return _cachedCamera;
    }

    private void CachePlayerComponents()
    {
        if (_playerHealth == null)
        {
            _playerHealth = GetComponent<AgentHealthController>();
            if (_playerHealth == null)
            {
                _playerHealth = GetComponentInParent<AgentHealthController>();
            }
        }

        if (_playerMovement == null)
        {
            _playerMovement = GetComponent<PlayerMovementController>();
            if (_playerMovement == null)
            {
                _playerMovement = GetComponentInParent<PlayerMovementController>();
            }
        }

        if (_playerShooting == null)
        {
            _playerShooting = GetComponent<PlayerShootingController>();
            if (_playerShooting == null)
            {
                _playerShooting = GetComponentInParent<PlayerShootingController>();
            }
        }

        if (_characterMotor == null)
        {
            _characterMotor = GetComponent<CharacterMotor>();
            if (_characterMotor == null)
            {
                _characterMotor = GetComponentInParent<CharacterMotor>();
            }
        }
    }

    private bool IsInputBlocked()
    {
        return RaidFlowController.Instance != null && RaidFlowController.Instance.IsInputLocked ||
               _playerShooting != null && _playerShooting.IsSilenced();
    }

    private GameObject CreateFollowAura(string objectName, Material material, float radius, float height)
    {
        GameObject root = new GameObject(objectName);
        root.layer = gameObject.layer;
        root.transform.position = transform.position + Vector3.up * 0.85f;

        LineRenderer ring = CreateLine(root.transform, "AuraRing", material, 0.08f, 8);
        ring.useWorldSpace = false;
        ApplyLine(ring, BuildCircle(Vector3.down * (0.85f - height), radius, 0f), Color.white, material.color, 0.85f, 0.08f);

        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        ConfigureAuraParticles(particles, material.color, radius);
        return root;
    }

    private void SpawnConeTelegraph(
        Vector3 origin,
        Vector3 forward,
        float radius,
        float angle,
        Material lineMaterial,
        Material boundaryMaterial,
        float duration)
    {
        GameObject root = new GameObject("PlayerConeSkillTelegraph");
        root.layer = gameObject.layer;

        LineRenderer arc = CreateLine(root.transform, "ConeArc", lineMaterial, 0.1f, 8);
        LineRenderer left = CreateLine(root.transform, "ConeLeft", boundaryMaterial, 0.055f, 9);
        LineRenderer right = CreateLine(root.transform, "ConeRight", boundaryMaterial, 0.055f, 9);

        Vector3 leftDirection = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * forward;
        Vector3 rightDirection = Quaternion.AngleAxis(angle * 0.5f, Vector3.up) * forward;
        ApplyLine(arc, BuildArc(origin, forward, radius, angle, _groundOffset), new Color(0.55f, 0.92f, 1f, 0.95f), Color.white, 0.9f, 0.1f);
        ApplyLine(left, new[] { origin + Vector3.up * _groundOffset, origin + leftDirection * radius + Vector3.up * _groundOffset }, Color.white, new Color(0.55f, 0.92f, 1f, 0.75f), 0.75f, 0.055f);
        ApplyLine(right, new[] { origin + Vector3.up * _groundOffset, origin + rightDirection * radius + Vector3.up * _groundOffset }, Color.white, new Color(0.55f, 0.92f, 1f, 0.75f), 0.75f, 0.055f);
        Destroy(root, duration);
    }

    private void SpawnGroundRing(string objectName, Vector3 center, float radius, Material material, float width, float duration)
    {
        GameObject root = new GameObject(objectName);
        root.layer = gameObject.layer;
        LineRenderer ring = CreateLine(root.transform, "Ring", material, width, 8);
        ApplyLine(ring, BuildCircle(center, radius, 0f), material.color, Color.white, 0.85f, width);
        Destroy(root, duration);
    }

    private void SpawnDirectionalParticles(string objectName, Vector3 position, Vector3 forward, Material material, float duration, int burstCount, float range)
    {
        GameObject root = new GameObject(objectName);
        root.layer = gameObject.layer;
        root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        PrepareParticleSystem(particles);

        ParticleSystem.MainModule main = particles.main;
        main.duration = duration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(range * 0.75f, range * 1.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(material.color, Color.white);
        main.maxParticles = burstCount * 2;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = _iceConeAngle * 0.5f;
        shape.radius = 0.2f;
        shape.length = 0.5f;

        ParticleSystemRenderer rendererComponent = particles.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = material;
        rendererComponent.sortingOrder = 8;

        particles.Play();
        Destroy(root, duration + 1f);
    }

    private void SpawnSmallBurst(Vector3 position, Color startColor, Color endColor, float scale)
    {
        GameObject root = new GameObject("PlayerSkillBurst");
        root.layer = gameObject.layer;
        root.transform.position = position;
        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        PrepareParticleSystem(particles);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.35f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f * scale, 3.2f * scale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f * scale, 0.28f * scale);
        main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)42) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.18f * scale;

        ParticleSystemRenderer rendererComponent = particles.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetSoftMaterial();
        rendererComponent.sortingOrder = 12;

        particles.Play();
        Destroy(root, 1.2f);
    }

    private void SpawnIceSpike(Vector3 position, Vector3 forward, float heightScale)
    {
        GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        spike.name = "PlayerIceSpike";
        spike.layer = gameObject.layer;
        spike.transform.position = position + Vector3.up * (0.35f + heightScale * 0.4f);
        spike.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(Random.Range(-18f, 18f), 0f, Random.Range(-8f, 8f));
        spike.transform.localScale = new Vector3(0.12f, heightScale, 0.12f);
        ApplyRenderer(spike, GetIceMaterial());
        RemoveCollider(spike);
        Destroy(spike, 0.95f);
    }

    private void SpawnIcePillar(Vector3 position, float height, float width, float lifetime)
    {
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "PlayerWinterIcePillar";
        pillar.layer = gameObject.layer;
        pillar.transform.position = position + Vector3.up * (height * 0.5f);
        pillar.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
        pillar.transform.localScale = new Vector3(width, height * 0.5f, width);
        ApplyRenderer(pillar, GetIceMaterial());
        RemoveCollider(pillar);
        Destroy(pillar, lifetime);
    }

    private void ConfigureTrailParticles(ParticleSystem particles, Color startColor, Color endColor, float lifetime, float size)
    {
        PrepareParticleSystem(particles);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 62f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        ParticleSystemRenderer rendererComponent = particles.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetSoftMaterial();
        rendererComponent.sortingOrder = 10;
    }

    private void ConfigureAuraParticles(ParticleSystem particles, Color color, float radius)
    {
        PrepareParticleSystem(particles);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.maxParticles = 150;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 52f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.arc = 360f;

        ParticleSystemRenderer rendererComponent = particles.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetSoftMaterial();
        rendererComponent.sortingOrder = 9;
        particles.Play();
    }

    private void PrepareParticleSystem(ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildFadeGradient(Color.white));
    }

    private Vector3[] BuildCircle(Vector3 center, float radius, float heightOffset)
    {
        Vector3[] points = new Vector3[_ringSegments + 1];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)_ringSegments * Mathf.PI * 2f;
            points[i] = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius + Vector3.up * heightOffset;
        }

        return points;
    }

    private Vector3[] BuildArc(Vector3 origin, Vector3 forward, float radius, float angle, float height)
    {
        Vector3[] points = new Vector3[_lineSegments + 1];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)_lineSegments;
            float currentAngle = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, t);
            Vector3 direction = Quaternion.AngleAxis(currentAngle, Vector3.up) * forward;
            points[i] = origin + direction * radius + Vector3.up * height;
        }

        return points;
    }

    private Vector3[] BuildCrack(Vector3 center, float radius, float rotation)
    {
        Vector3[] points = new Vector3[7];
        Vector3 direction = Quaternion.AngleAxis(rotation, Vector3.up) * Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            points[i] = center + direction * Mathf.Lerp(-radius * 0.82f, radius * 0.82f, t) + side * Mathf.Sin(t * Mathf.PI * 5f) * 0.18f + Vector3.up * (_groundOffset + 0.02f);
        }

        return points;
    }

    private Vector3[] BuildVerticalCircle(Vector3 center, float radius, Vector3 facing)
    {
        Vector3 normal = facing;
        normal.y = 0f;
        if (normal.sqrMagnitude <= 0.0001f)
        {
            normal = Vector3.forward;
        }

        Vector3 right = Vector3.Cross(Vector3.up, normal.normalized).normalized;
        Vector3 up = Vector3.up;
        Vector3[] points = new Vector3[_ringSegments + 1];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = i / (float)_ringSegments * Mathf.PI * 2f;
            points[i] = center + right * Mathf.Cos(angle) * radius + up * Mathf.Sin(angle) * radius;
        }

        return points;
    }

    private Vector3[] BuildVerticalSpark(Vector3 center, float radius, float angle)
    {
        Vector3 right = transform.right.sqrMagnitude > 0.0001f ? transform.right.normalized : Vector3.right;
        Vector3 up = Vector3.up;
        Vector3 start = center + right * Mathf.Cos(angle * Mathf.Deg2Rad) * radius + up * Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
        Vector3 end = center - right * Mathf.Cos(angle * Mathf.Deg2Rad) * radius - up * Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
        return new[] { start, end };
    }

    private void ApplyLine(LineRenderer line, Vector3[] points, Color startColor, Color endColor, float alpha, float width)
    {
        if (line == null || points == null || points.Length < 2)
        {
            return;
        }

        line.positionCount = points.Length;
        line.SetPositions(points);
        startColor.a *= alpha;
        endColor.a *= alpha;
        line.startColor = startColor;
        line.endColor = endColor;
        line.widthMultiplier = width;
        line.enabled = alpha > 0.01f;
    }

    private LineRenderer CreateLine(Transform parent, string objectName, Material material, float width, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.layer = gameObject.layer;
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = width;
        line.numCapVertices = 6;
        line.numCornerVertices = 4;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = sortingOrder;
        line.material = material;
        return line;
    }

    private Material GetSoftMaterial()
    {
        if (_softMaterial == null)
        {
            _softMaterial = CreateMaterial(new Color(0.86f, 0.97f, 1f, 0.85f));
        }

        return _softMaterial;
    }

    private Material GetIceMaterial()
    {
        if (_iceMaterial == null)
        {
            _iceMaterial = CreateMaterial(new Color(0.45f, 0.9f, 1f, 0.86f));
        }

        return _iceMaterial;
    }

    private Material GetEarthMaterial()
    {
        if (_earthMaterial == null)
        {
            _earthMaterial = CreateMaterial(new Color(0.55f, 0.38f, 0.2f, 0.88f));
        }

        return _earthMaterial;
    }

    private Material GetFireMaterial()
    {
        if (_fireMaterial == null)
        {
            _fireMaterial = CreateMaterial(new Color(1f, 0.42f, 0.08f, 0.88f));
        }

        return _fireMaterial;
    }

    private Material GetMetalMaterial()
    {
        if (_metalMaterial == null)
        {
            _metalMaterial = CreateMaterial(new Color(0.95f, 0.78f, 0.32f, 0.9f));
        }

        return _metalMaterial;
    }

    private Material GetWaterMaterial()
    {
        if (_waterMaterial == null)
        {
            _waterMaterial = CreateMaterial(new Color(0.18f, 0.8f, 1f, 0.82f));
        }

        return _waterMaterial;
    }

    private Material GetDarkLineMaterial()
    {
        if (_darkLineMaterial == null)
        {
            _darkLineMaterial = CreateMaterial(new Color(0.08f, 0.06f, 0.05f, 0.82f));
        }

        return _darkLineMaterial;
    }

    private Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            color = color
        };
        material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        material.renderQueue = 3000;
        return material;
    }

    private static Gradient BuildFadeGradient(Color color)
    {
        Gradient gradient = new Gradient();
        Color transparent = color;
        transparent.a = 0f;
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 0.55f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(color.a, 0.2f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static void ApplyRenderer(GameObject target, Material material)
    {
        Renderer rendererComponent = target.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.sharedMaterial = material;
            rendererComponent.shadowCastingMode = ShadowCastingMode.Off;
            rendererComponent.receiveShadows = false;
        }
    }

    private static void RemoveCollider(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private static void DestroyRuntimeMaterial(ref Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }

        material = null;
    }

    private void LogPreview(string message)
    {
        if (_logCooldown > 0f)
        {
            return;
        }

        _logCooldown = 0.8f;
        Debug.Log(message, this);
    }

    private static readonly List<EnemyHealthController> s_removedTargets = new List<EnemyHealthController>(16);
    private static readonly List<EnemyHealthController> s_expiredTargets = new List<EnemyHealthController>(16);
}
