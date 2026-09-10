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
    [SerializeField, Range(0.8f, 4f)] private float _effectIntensityScale = 2.2f;
    [SerializeField, Range(0.8f, 4f)] private float _lineWidthScale = 1.85f;
    [SerializeField, Range(0.8f, 4f)] private float _burstDensityScale = 2.35f;
    [SerializeField, Range(0.5f, 3f)] private float _runeGlowScale = 1.35f;
    [SerializeField, Range(0.5f, 3f)] private float _crystalVisualScale = 1.35f;

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

    public void SetKeyboardPreviewEnabled(bool enabled)
    {
        _enableKeyboardPreview = enabled;
    }

    public void PlayPrototypeIceFrostAssaultVisual(Vector3 origin, Vector3 forward, float radius, float angleDegrees)
    {
        Vector3 safeForward = ResolveSafePlanarDirection(forward);
        float safeRadius = Mathf.Max(0.1f, radius);
        float safeAngle = Mathf.Clamp(angleDegrees, 1f, 360f);
        SpawnFrostAssaultSpectacle(origin, safeForward, safeRadius, safeAngle, 0.9f);
    }

    public void PlayPrototypeIceWinterVisual(Vector3 target, float radius)
    {
        float safeRadius = Mathf.Max(0.1f, radius);
        target.y += _groundOffset;
        SpawnWinterfallSpectacle(target, safeRadius, 0.95f);
        StartCoroutine(WinterVisualRoutine(target, safeRadius));
    }

    public void PlayPrototypeEarthWallVisual(
        Vector3 center,
        Vector3 forward,
        float length,
        float width,
        float height,
        float durationSeconds)
    {
        Vector3 safeForward = ResolveSafePlanarDirection(forward);
        float safeLength = Mathf.Max(0.1f, length);
        float safeWidth = Mathf.Max(0.1f, width);
        float safeHeight = Mathf.Max(0.1f, height);
        GameObject wall = SpawnEarthWallSpectacle(
            "PrototypePlayerEarthWall",
            center,
            safeForward,
            safeLength,
            safeWidth,
            safeHeight,
            false);
        Destroy(wall, Mathf.Max(0.05f, durationSeconds));
    }

    public void PlayPrototypeEarthQuakeVisual(Vector3 target, float radius, float durationSeconds)
    {
        float safeRadius = Mathf.Max(0.1f, radius);
        float safeDuration = Mathf.Max(0.05f, durationSeconds);
        SpawnQuakeOpeningSpectacle(target, safeRadius, safeDuration);
        StartCoroutine(EarthQuakeVisualRoutine(target, safeRadius, safeDuration));
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

        SpawnFrostAssaultSpectacle(origin, forward, _iceConeRadius, _iceConeAngle, 1f);

        CollectEnemiesInCone(origin, forward, _iceConeRadius, _iceConeAngle, _targets);
        foreach (EnemyHealthController enemy in _targets)
        {
            ApplyEnemyDamage(enemy, AttackDamage(_iceConeDamageMultiplier), enemy.transform.position, origin);
            GetOrCreateEnemyStatus(enemy).ApplySlow(_iceSlowMultiplier, _iceSlowDuration);
            SpawnSmallBurst(enemy.transform.position + Vector3.up * 0.75f, Color.white, new Color(0.45f, 0.9f, 1f, 0.65f), 1f);
        }

        return true;
    }

    private bool CastWinter()
    {
        Vector3 target = ResolveAimPoint(_winterRadius + 4f);
        target.y += _groundOffset;
        SpawnWinterfallSpectacle(target, _winterRadius, 1.1f);
        StartCoroutine(WinterRoutine(target));
        return true;
    }

    private IEnumerator WinterRoutine(Vector3 target)
    {
        SpawnSmallBurst(target + Vector3.up * 2.9f, new Color(0.74f, 0.96f, 1f, 0.8f), Color.white, 1.45f);
        yield return new WaitForSeconds(0.22f);

        CollectEnemiesInSphere(target, _winterRadius, _targets);
        foreach (EnemyHealthController enemy in _targets)
        {
            ApplyEnemyDamage(enemy, AttackDamage(_winterDamageMultiplier), enemy.transform.position, target);
        }

        int pillarCount = 13;
        for (int i = 0; i < pillarCount; i++)
        {
            float angle = i * (360f / pillarCount);
            float radius = i == 0 ? 0f : Random.Range(_winterRadius * 0.2f, _winterRadius * 0.78f);
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
            SpawnIcePillar(target + offset, Random.Range(1.35f, 2.65f), Random.Range(0.16f, 0.34f), Random.Range(0.9f, 1.25f));
        }
    }

    private IEnumerator WinterVisualRoutine(Vector3 target, float radius)
    {
        SpawnSmallBurst(target + Vector3.up * 2.9f, new Color(0.74f, 0.96f, 1f, 0.8f), Color.white, 1f);
        yield return new WaitForSeconds(0.22f);

        int pillarCount = 9;
        for (int i = 0; i < pillarCount; i++)
        {
            float angle = i * (360f / pillarCount);
            float pillarRadius = i == 0 ? 0f : Random.Range(radius * 0.2f, radius * 0.78f);
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * pillarRadius;
            SpawnIcePillar(target + offset, Random.Range(1.35f, 2.65f), Random.Range(0.16f, 0.34f), Random.Range(0.9f, 1.25f));
        }
    }

    private bool CastEarthWall()
    {
        Vector3 forward = FlattenedForward();
        Vector3 center = transform.position + forward * 2.15f + Vector3.up * (_earthWallHeight * 0.5f);
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        GameObject wall = SpawnEarthWallSpectacle(
            "PlayerEarthWall",
            center,
            forward,
            _earthWallLength,
            _earthWallWidth,
            _earthWallHeight,
            true);

        Rigidbody wallBody = wall.AddComponent<Rigidbody>();
        wallBody.isKinematic = true;
        wallBody.useGravity = false;

        DamageEnemiesInWallBox(center, rotation, DefenseDamage(_earthWallDamageMultiplier));
        Destroy(wall, _earthWallDuration);
        return true;
    }

    private bool CastEarthQuakeInternal()
    {
        Vector3 target = ResolveAimPoint(_earthQuakeRadius + 4f);
        SpawnQuakeOpeningSpectacle(target, _earthQuakeRadius, _earthQuakeDuration);
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

        LineRenderer outer = CreateLine(root.transform, "EarthQuakeOuter", GetEarthMaterial(), 0.18f, 8);
        LineRenderer inner = CreateLine(root.transform, "EarthQuakeInner", GetDarkLineMaterial(), 0.1f, 9);
        LineRenderer crackA = CreateLine(root.transform, "EarthCrackA", GetDarkLineMaterial(), 0.13f, 10);
        LineRenderer crackB = CreateLine(root.transform, "EarthCrackB", GetDarkLineMaterial(), 0.13f, 10);

        while (elapsed < _earthQuakeDuration)
        {
            elapsed += Time.deltaTime;
            tick -= Time.deltaTime;
            float pulse = 0.85f + Mathf.Sin(Time.time * 12f) * 0.15f;
            ApplyLine(outer, BuildCircle(target, _earthQuakeRadius * pulse, _groundOffset), new Color(0.78f, 0.58f, 0.28f, 0.95f), new Color(0.36f, 0.2f, 0.1f, 0.78f), 0.95f, 0.18f);
            ApplyLine(inner, BuildCircle(target, _earthQuakeRadius * 0.58f, _groundOffset + 0.01f), new Color(1f, 0.82f, 0.38f, 0.95f), new Color(0.22f, 0.13f, 0.08f, 0.8f), 0.92f, 0.1f);
            ApplyLine(crackA, BuildCrack(target, _earthQuakeRadius, Time.time * 25f), Color.black, new Color(0.95f, 0.62f, 0.22f, 0.82f), 0.86f, 0.13f);
            ApplyLine(crackB, BuildCrack(target, _earthQuakeRadius, Time.time * -18f + 90f), Color.black, new Color(0.95f, 0.62f, 0.22f, 0.82f), 0.86f, 0.13f);

            if (tick <= 0f)
            {
                tick = 1f;
                CollectEnemiesInSphere(target, _earthQuakeRadius, _targets);
                foreach (EnemyHealthController enemy in _targets)
                {
                    ApplyEnemyDamage(enemy, DefenseDamage(_earthQuakeDamageMultiplier), enemy.transform.position, target);
                }

                SpawnSmallBurst(target + Vector3.up * 0.12f, new Color(0.58f, 0.39f, 0.18f, 0.85f), new Color(0.95f, 0.65f, 0.25f, 0.45f), 0.9f);
                SpawnRadialParticleBurst(
                    "QuakePulseDebris",
                    target + Vector3.up * 0.16f,
                    GetEarthMaterial(),
                    new Color(0.72f, 0.48f, 0.2f, 0.82f),
                    new Color(0.18f, 0.1f, 0.06f, 0.32f),
                    _earthQuakeRadius,
                    0.72f,
                    120,
                    0.82f);
                for (int shardIndex = 0; shardIndex < 7; shardIndex++)
                {
                    float angle = Random.Range(0f, 360f);
                    float shardRadius = Random.Range(_earthQuakeRadius * 0.18f, _earthQuakeRadius * 0.88f);
                    Vector3 shardPosition = target + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * shardRadius;
                    SpawnEarthShard(shardPosition, Random.Range(0.35f, 0.9f), Random.Range(0.45f, 0.9f));
                }
            }

            yield return null;
        }

        Destroy(root);
    }

    private IEnumerator EarthQuakeVisualRoutine(Vector3 target, float radius, float durationSeconds)
    {
        float elapsed = 0f;
        float burstTick = 0f;
        GameObject root = new GameObject("PrototypePlayerEarthQuakeArray");
        root.layer = gameObject.layer;
        root.transform.position = target;

        LineRenderer outer = CreateLine(root.transform, "EarthQuakeOuter", GetEarthMaterial(), 0.12f, 8);
        LineRenderer inner = CreateLine(root.transform, "EarthQuakeInner", GetDarkLineMaterial(), 0.06f, 9);
        LineRenderer crackA = CreateLine(root.transform, "EarthCrackA", GetDarkLineMaterial(), 0.08f, 10);
        LineRenderer crackB = CreateLine(root.transform, "EarthCrackB", GetDarkLineMaterial(), 0.08f, 10);

        while (elapsed < durationSeconds)
        {
            elapsed += Time.deltaTime;
            burstTick -= Time.deltaTime;
            float pulse = 0.85f + Mathf.Sin(Time.time * 12f) * 0.15f;
            ApplyLine(outer, BuildCircle(target, radius * pulse, _groundOffset), new Color(0.72f, 0.55f, 0.28f, 0.9f), new Color(0.36f, 0.2f, 0.1f, 0.65f), 0.85f, 0.12f);
            ApplyLine(inner, BuildCircle(target, radius * 0.58f, _groundOffset + 0.01f), new Color(0.95f, 0.76f, 0.35f, 0.85f), new Color(0.22f, 0.13f, 0.08f, 0.7f), 0.8f, 0.06f);
            ApplyLine(crackA, BuildCrack(target, radius, Time.time * 25f), Color.black, new Color(0.85f, 0.55f, 0.2f, 0.7f), 0.75f, 0.08f);
            ApplyLine(crackB, BuildCrack(target, radius, Time.time * -18f + 90f), Color.black, new Color(0.85f, 0.55f, 0.2f, 0.7f), 0.75f, 0.08f);

            if (burstTick <= 0f)
            {
                burstTick = 1f;
                SpawnSmallBurst(target + Vector3.up * 0.12f, new Color(0.58f, 0.39f, 0.18f, 0.85f), new Color(0.95f, 0.65f, 0.25f, 0.45f), 0.9f);
                SpawnRadialParticleBurst(
                    "PrototypeQuakePulseDebris",
                    target + Vector3.up * 0.16f,
                    GetEarthMaterial(),
                    new Color(0.72f, 0.48f, 0.2f, 0.78f),
                    new Color(0.18f, 0.1f, 0.06f, 0.28f),
                    radius,
                    0.72f,
                    100,
                    0.75f);
                for (int shardIndex = 0; shardIndex < 5; shardIndex++)
                {
                    float angle = Random.Range(0f, 360f);
                    float shardRadius = Random.Range(radius * 0.18f, radius * 0.88f);
                    Vector3 shardPosition = target + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * shardRadius;
                    SpawnEarthShard(shardPosition, Random.Range(0.32f, 0.78f), Random.Range(0.45f, 0.85f));
                }
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

        SpawnSmallBurst(transform.position + Vector3.up * 0.9f, new Color(1f, 0.5f, 0.16f, 0.9f), new Color(1f, 0.94f, 0.54f, 0.65f), 1.6f);
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
        projectile.transform.localScale = Vector3.one * (_fireballRadius * 2.65f);
        ApplyRenderer(projectile, GetFireMaterial());
        RemoveCollider(projectile);

        ParticleSystem trail = projectile.AddComponent<ParticleSystem>();
        ConfigureTrailParticles(trail, new Color(1f, 0.45f, 0.08f, 0.9f), new Color(1f, 0.9f, 0.35f, 0.55f), 0.34f, 0.18f);
        LineRenderer coreTrail = CreateLine(projectile.transform, "FireballCoreTrail", GetFireMaterial(), 0.16f, 14);

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
                SpawnSmallBurst(hitPoint, new Color(1f, 0.42f, 0.05f, 0.95f), new Color(1f, 0.94f, 0.45f, 0.8f), 1.65f);
                Destroy(projectile);
                yield break;
            }

            projectile.transform.position = next;
            ApplyLine(coreTrail, new[] { next - direction * 1.15f, next + direction * 0.16f }, new Color(1f, 0.24f, 0.02f, 0.85f), new Color(1f, 0.96f, 0.48f, 0.95f), 1f, 0.16f);
            previous = next;
            yield return null;
        }

        SpawnSmallBurst(projectile.transform.position, new Color(1f, 0.42f, 0.05f, 0.85f), new Color(1f, 0.94f, 0.45f, 0.55f), 1.25f);
        Destroy(projectile);
    }

    private bool CastMetalBuffInternal()
    {
        _metalBuffRemaining = Mathf.Max(_metalBuffRemaining, _metalBuffDuration);
        if (_metalBuffVisual == null)
        {
            _metalBuffVisual = CreateFollowAura("PlayerMetalBuffAura", GetMetalMaterial(), 1.65f, 0.1f);
        }

        SpawnSmallBurst(transform.position + Vector3.up * 0.8f, new Color(0.95f, 0.86f, 0.48f, 0.9f), new Color(1f, 1f, 0.85f, 0.7f), 1.45f);
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
        SpawnSmallBurst(enemy.transform.position + Vector3.up * 1f, new Color(1f, 0.72f, 0.24f, 0.9f), new Color(1f, 0.96f, 0.7f, 0.7f), 1.2f);
        return true;
    }

    private IEnumerator SmeltMarkRoutine(EnemyHealthController enemy, float duration)
    {
        GameObject root = new GameObject("PlayerSmeltVulnerableMark");
        root.layer = gameObject.layer;
        LineRenderer ring = CreateLine(root.transform, "SmeltRing", GetMetalMaterial(), 0.13f, 10);
        LineRenderer spark = CreateLine(root.transform, "SmeltSpark", GetFireMaterial(), 0.08f, 11);
        float elapsed = 0f;

        while (elapsed < duration && enemy != null && enemy.IsAlive)
        {
            elapsed += Time.deltaTime;
            Vector3 center = enemy.transform.position + Vector3.up * 1.1f;
            float radius = 0.65f + Mathf.Sin(Time.time * 7f) * 0.08f;
            ApplyLine(ring, BuildVerticalCircle(center, radius, transform.forward), new Color(1f, 0.74f, 0.22f, 0.95f), new Color(1f, 0.96f, 0.64f, 0.84f), 0.96f, 0.13f);
            ApplyLine(spark, BuildVerticalSpark(center, radius, Time.time * 110f), new Color(1f, 0.4f, 0.08f, 0.95f), Color.white, 0.92f, 0.08f);
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

        SpawnGroundRing("PlayerWaterConfluenceRing", transform.position + Vector3.up * _groundOffset, _confluenceRadius, GetWaterMaterial(), 0.2f, _confluenceDuration);
        SpawnSmallBurst(transform.position + Vector3.up * 0.8f, new Color(0.18f, 0.85f, 1f, 0.9f), new Color(0.82f, 1f, 1f, 0.72f), 1.45f);
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
        LineRenderer ring = CreateLine(root.transform, "SpringRing", GetWaterMaterial(), 0.14f, 11);
        LineRenderer innerRing = CreateLine(root.transform, "SpringInnerRing", GetSoftMaterial(), 0.07f, 12);

        while (elapsed < _springDuration && target != null && !target.IsDead)
        {
            elapsed += Time.deltaTime;
            tick -= Time.deltaTime;
            Vector3 center = target.transform.position + Vector3.up * _groundOffset;
            root.transform.position = center;
            ApplyLine(ring, BuildCircle(center, 1.18f + Mathf.Sin(Time.time * 8f) * 0.16f, 0.02f), new Color(0.36f, 0.94f, 1f, 0.95f), Color.white, 0.96f, 0.14f);
            ApplyLine(innerRing, BuildCircle(center, 0.62f + Mathf.Sin(Time.time * 12f) * 0.08f, 0.06f), Color.white, new Color(0.22f, 0.9f, 1f, 0.85f), 0.82f, 0.07f);

            if (tick <= 0f)
            {
                tick = 1f;
                target.Heal(ResolveMaxHealth(target) * _springHealMaxHealthRatio);
                SpawnSmallBurst(target.transform.position + Vector3.up * 1f, new Color(0.32f, 0.9f, 1f, 0.85f), Color.white, 1.05f);
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
        return ResolveSafePlanarDirection(transform.forward);
    }

    private static Vector3 ResolveSafePlanarDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        return direction.normalized;
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

        LineRenderer outerRing = CreateLine(root.transform, "AuraOuterRing", material, 0.13f, 8);
        LineRenderer innerRing = CreateLine(root.transform, "AuraInnerRing", GetSoftMaterial(), 0.07f, 9);
        LineRenderer upperRing = CreateLine(root.transform, "AuraUpperRing", material, 0.075f, 10);
        outerRing.useWorldSpace = false;
        innerRing.useWorldSpace = false;
        upperRing.useWorldSpace = false;

        Vector3 groundCenter = Vector3.down * (0.85f - height);
        ApplyLine(outerRing, BuildCircle(groundCenter, radius, 0f), Color.white, material.color, 0.96f, 0.13f);
        ApplyLine(innerRing, BuildCircle(groundCenter, radius * 0.62f, 0.02f), material.color, Color.white, 0.78f, 0.07f);
        ApplyLine(upperRing, BuildCircle(Vector3.up * 0.18f, radius * 0.42f, 0f), Color.white, material.color, 0.66f, 0.075f);

        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        ConfigureAuraParticles(particles, material.color, radius);
        return root;
    }

    private void SpawnFrostAssaultSpectacle(
        Vector3 origin,
        Vector3 forward,
        float radius,
        float angle,
        float scale)
    {
        float safeRadius = Mathf.Max(0.1f, radius);
        float safeAngle = Mathf.Clamp(angle, 1f, 360f);
        float visualScale = Mathf.Max(0.1f, scale);
        Vector3 safeForward = ResolveSafePlanarDirection(forward);

        SpawnConeTelegraph(origin, safeForward, safeRadius, safeAngle, GetIceMaterial(), GetSoftMaterial(), 0.92f);
        SpawnConeRune(origin, safeForward, safeRadius, safeAngle, GetIceMaterial(), GetSoftMaterial(), 0.86f);
        SpawnDirectionalParticles(
            "FrostAssaultBlizzardFan",
            origin + Vector3.up * 0.28f,
            safeForward,
            GetIceMaterial(),
            0.72f,
            Mathf.RoundToInt(150f * visualScale),
            safeRadius,
            safeAngle);
        SpawnDirectionalParticles(
            "FrostAssaultSilverMist",
            origin + Vector3.up * 0.55f,
            safeForward,
            GetSoftMaterial(),
            0.58f,
            Mathf.RoundToInt(92f * visualScale),
            safeRadius * 0.72f,
            safeAngle * 0.72f);
        SpawnSmallBurst(
            origin + Vector3.up * 0.55f,
            new Color(0.62f, 0.94f, 1f, 0.95f),
            Color.white,
            1.25f * visualScale);

        int spikeCount = Mathf.Max(18, Mathf.RoundToInt(safeAngle / 5.5f));
        for (int i = 0; i < spikeCount; i++)
        {
            float t = spikeCount == 1 ? 0.5f : i / (float)(spikeCount - 1);
            float currentAngle = Mathf.Lerp(-safeAngle * 0.5f, safeAngle * 0.5f, t);
            float distanceT = Mathf.PingPong(i * 0.41f + 0.17f, 1f);
            float distance = Mathf.Lerp(safeRadius * 0.22f, safeRadius * 0.96f, distanceT);
            Vector3 direction = Quaternion.AngleAxis(currentAngle, Vector3.up) * safeForward;
            SpawnIceSpike(
                origin + direction * distance + Vector3.up * _groundOffset,
                direction,
                (0.55f + distanceT * 0.52f) * visualScale);
        }
    }

    private void SpawnWinterfallSpectacle(Vector3 center, float radius, float scale)
    {
        float safeRadius = Mathf.Max(0.1f, radius);
        float visualScale = Mathf.Max(0.1f, scale);
        SpawnLayeredGroundRune(
            "WinterfallRune",
            center,
            safeRadius,
            GetIceMaterial(),
            GetSoftMaterial(),
            new Color(0.56f, 0.95f, 1f, 0.92f),
            1.25f);
        SpawnVerticalStrike(center, safeRadius, GetIceMaterial(), 1.05f);
        SpawnRadialParticleBurst(
            "WinterfallSnowCrown",
            center + Vector3.up * (safeRadius * 0.45f),
            GetSoftMaterial(),
            new Color(0.72f, 0.96f, 1f, 0.95f),
            Color.white,
            safeRadius,
            1.2f,
            Mathf.RoundToInt(230f * visualScale),
            0.8f * visualScale);
        SpawnSmallBurst(
            center + Vector3.up * (safeRadius * 0.85f),
            new Color(0.72f, 0.96f, 1f, 0.95f),
            Color.white,
            1.35f * visualScale);
    }

    private GameObject SpawnEarthWallSpectacle(
        string objectName,
        Vector3 center,
        Vector3 forward,
        float length,
        float width,
        float height,
        bool keepBlockColliders)
    {
        Vector3 safeForward = ResolveSafePlanarDirection(forward);
        Quaternion rotation = Quaternion.LookRotation(safeForward, Vector3.up);
        GameObject wall = new GameObject(objectName);
        wall.layer = gameObject.layer;
        wall.transform.SetPositionAndRotation(center, rotation);

        float safeLength = Mathf.Max(0.1f, length);
        float safeWidth = Mathf.Max(0.1f, width);
        float safeHeight = Mathf.Max(0.1f, height);
        int blockCount = Mathf.Max(9, Mathf.RoundToInt(safeLength * 2.5f));
        for (int i = 0; i < blockCount; i++)
        {
            float t = blockCount == 1 ? 0.5f : i / (float)(blockCount - 1);
            float x = Mathf.Lerp(-safeLength * 0.5f, safeLength * 0.5f, t);
            float crest = Mathf.Sin(t * Mathf.PI) * 0.22f + Random.Range(-0.06f, 0.12f);
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "EarthWallObelisk";
            block.layer = gameObject.layer;
            block.transform.SetParent(wall.transform, false);
            block.transform.localPosition = new Vector3(
                x,
                crest,
                Random.Range(-safeWidth * 0.16f, safeWidth * 0.16f));
            block.transform.localRotation = Quaternion.Euler(
                Random.Range(-5f, 8f),
                Random.Range(-11f, 11f),
                Random.Range(-7f, 7f));
            block.transform.localScale = new Vector3(
                safeLength / blockCount * Random.Range(1.08f, 1.48f),
                safeHeight * Random.Range(0.72f, 1.18f),
                safeWidth * Random.Range(0.85f, 1.32f));
            ApplyRenderer(block, GetEarthMaterial());
            if (!keepBlockColliders)
            {
                RemoveCollider(block);
            }
        }

        Vector3 groundCenter = center - Vector3.up * (safeHeight * 0.5f - _groundOffset);
        SpawnLayeredGroundRune(
            "EarthWallFaultRune",
            groundCenter,
            safeLength * 0.64f,
            GetEarthMaterial(),
            GetDarkLineMaterial(),
            new Color(1f, 0.72f, 0.32f, 0.88f),
            1.05f);
        SpawnWallDustCurtain(groundCenter, safeForward, safeLength, safeWidth, safeHeight);
        SpawnSmallBurst(
            center,
            new Color(0.62f, 0.42f, 0.18f, 0.92f),
            new Color(1f, 0.78f, 0.34f, 0.56f),
            1.75f);

        for (int i = 0; i < 12; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Vector3 right = Vector3.Cross(Vector3.up, safeForward).normalized;
            Vector3 shardPosition =
                groundCenter +
                right * Random.Range(-safeLength * 0.5f, safeLength * 0.5f) +
                safeForward * (side * Random.Range(safeWidth * 0.55f, safeWidth * 1.6f));
            SpawnEarthShard(shardPosition, Random.Range(0.55f, 1.2f), Random.Range(0.65f, 1.25f));
        }

        return wall;
    }

    private void SpawnQuakeOpeningSpectacle(Vector3 center, float radius, float duration)
    {
        float safeRadius = Mathf.Max(0.1f, radius);
        SpawnLayeredGroundRune(
            "QuakeImpactSigil",
            center + Vector3.up * _groundOffset,
            safeRadius,
            GetEarthMaterial(),
            GetDarkLineMaterial(),
            new Color(1f, 0.72f, 0.28f, 0.9f),
            Mathf.Min(1.3f, duration));
        SpawnRadialParticleBurst(
            "QuakeOpeningDust",
            center + Vector3.up * 0.16f,
            GetEarthMaterial(),
            new Color(0.84f, 0.56f, 0.25f, 0.88f),
            new Color(0.18f, 0.1f, 0.06f, 0.42f),
            safeRadius,
            0.95f,
            220,
            1.15f);
        SpawnSmallBurst(
            center + Vector3.up * 0.24f,
            new Color(0.78f, 0.5f, 0.18f, 0.96f),
            new Color(1f, 0.82f, 0.34f, 0.62f),
            1.55f);
    }

    private void SpawnConeRune(
        Vector3 origin,
        Vector3 forward,
        float radius,
        float angle,
        Material primary,
        Material secondary,
        float duration)
    {
        GameObject root = new GameObject("FrostAssaultRunicFan");
        root.layer = gameObject.layer;
        LineRenderer farArc = CreateLine(root.transform, "FarArc", primary, 0.13f, 16);
        LineRenderer midArc = CreateLine(root.transform, "MidArc", secondary, 0.08f, 17);
        LineRenderer nearArc = CreateLine(root.transform, "NearArc", primary, 0.06f, 18);

        ApplyLine(farArc, BuildArc(origin, forward, radius, angle, _groundOffset + 0.04f), Color.white, primary.color, 0.95f, 0.13f);
        ApplyLine(midArc, BuildArc(origin, forward, radius * 0.66f, angle * 0.78f, _groundOffset + 0.08f), primary.color, Color.white, 0.72f, 0.08f);
        ApplyLine(nearArc, BuildArc(origin, forward, radius * 0.34f, angle * 0.52f, _groundOffset + 0.12f), Color.white, primary.color, 0.5f, 0.06f);

        int spokeCount = 7;
        for (int i = 0; i < spokeCount; i++)
        {
            float t = spokeCount == 1 ? 0.5f : i / (float)(spokeCount - 1);
            float currentAngle = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, t);
            Vector3 direction = Quaternion.AngleAxis(currentAngle, Vector3.up) * forward;
            LineRenderer spoke = CreateLine(root.transform, $"FanSpoke_{i:00}", i % 2 == 0 ? primary : secondary, 0.055f, 19);
            ApplyLine(
                spoke,
                new[]
                {
                    origin + direction * (radius * 0.16f) + Vector3.up * (_groundOffset + 0.04f),
                    origin + direction * (radius * Random.Range(0.74f, 0.98f)) + Vector3.up * (_groundOffset + 0.06f)
                },
                Color.white,
                primary.color,
                i % 2 == 0 ? 0.64f : 0.42f,
                0.055f);
        }

        Destroy(root, duration);
    }

    private void SpawnLayeredGroundRune(
        string objectName,
        Vector3 center,
        float radius,
        Material primary,
        Material secondary,
        Color accent,
        float duration)
    {
        float safeRadius = Mathf.Max(0.1f, radius);
        GameObject root = new GameObject(objectName);
        root.layer = gameObject.layer;

        LineRenderer outer = CreateLine(root.transform, "OuterRune", primary, 0.16f * _runeGlowScale, 11);
        LineRenderer outerGlow = CreateLine(root.transform, "OuterGlow", secondary, 0.08f * _runeGlowScale, 12);
        LineRenderer middle = CreateLine(root.transform, "MiddleRune", primary, 0.085f * _runeGlowScale, 13);
        LineRenderer inner = CreateLine(root.transform, "InnerRune", secondary, 0.055f * _runeGlowScale, 14);

        ApplyLine(outer, BuildCircle(center, safeRadius, _groundOffset + 0.02f), accent, Color.white, 0.95f, 0.16f * _runeGlowScale);
        ApplyLine(outerGlow, BuildCircle(center, safeRadius * 0.9f, _groundOffset + 0.04f), Color.white, accent, 0.58f, 0.08f * _runeGlowScale);
        ApplyLine(middle, BuildCircle(center, safeRadius * 0.62f, _groundOffset + 0.06f), accent, Color.white, 0.72f, 0.085f * _runeGlowScale);
        ApplyLine(inner, BuildCircle(center, safeRadius * 0.33f, _groundOffset + 0.08f), Color.white, accent, 0.52f, 0.055f * _runeGlowScale);

        int spokeCount = 10;
        for (int i = 0; i < spokeCount; i++)
        {
            float angle = i * (360f / spokeCount) + (i % 2 == 0 ? 0f : 9f);
            LineRenderer spoke = CreateLine(root.transform, $"RuneSpoke_{i:00}", i % 2 == 0 ? primary : secondary, 0.045f, 15);
            ApplyLine(
                spoke,
                BuildRadialSegment(center, angle, safeRadius * 0.36f, safeRadius * 0.94f, _groundOffset + 0.1f),
                i % 2 == 0 ? accent : Color.white,
                i % 2 == 0 ? Color.white : accent,
                i % 2 == 0 ? 0.52f : 0.36f,
                0.045f);
        }

        Destroy(root, duration);
    }

    private void SpawnRadialParticleBurst(
        string objectName,
        Vector3 position,
        Material material,
        Color startColor,
        Color endColor,
        float radius,
        float duration,
        int burstCount,
        float scale)
    {
        GameObject root = new GameObject(objectName);
        root.layer = gameObject.layer;
        root.transform.position = position;

        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        PrepareParticleSystem(particles);

        float safeRadius = Mathf.Max(0.1f, radius);
        float visualScale = Mathf.Max(0.1f, scale) * _effectIntensityScale;
        ParticleSystem.MainModule main = particles.main;
        main.duration = Mathf.Max(0.05f, duration);
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.38f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(safeRadius * 0.34f, safeRadius * 1.24f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.055f * visualScale, 0.22f * visualScale);
        main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.RoundToInt(Mathf.Max(16, burstCount * _burstDensityScale * 1.5f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, ResolveBurstCount(burstCount)) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = safeRadius * 0.16f;

        ParticleSystemRenderer rendererComponent = particles.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = material != null ? material : GetSoftMaterial();
        rendererComponent.sortingOrder = 18;

        particles.Play();
        Destroy(root, duration + 1.4f);
    }

    private void SpawnWallDustCurtain(Vector3 groundCenter, Vector3 forward, float length, float width, float height)
    {
        Vector3 safeForward = ResolveSafePlanarDirection(forward);
        Vector3 right = Vector3.Cross(Vector3.up, safeForward).normalized;
        int plumeCount = Mathf.Max(5, Mathf.RoundToInt(length * 1.8f));
        for (int i = 0; i < plumeCount; i++)
        {
            float t = plumeCount == 1 ? 0.5f : i / (float)(plumeCount - 1);
            Vector3 plumePosition =
                groundCenter +
                right * Mathf.Lerp(-length * 0.5f, length * 0.5f, t) +
                safeForward * Random.Range(-width, width) +
                Vector3.up * Random.Range(0.05f, height * 0.18f);
            SpawnRadialParticleBurst(
                "EarthWallDustPlume",
                plumePosition,
                GetEarthMaterial(),
                new Color(0.66f, 0.45f, 0.22f, 0.86f),
                new Color(0.18f, 0.11f, 0.07f, 0.34f),
                Mathf.Max(0.35f, width * 1.4f),
                0.75f,
                34,
                0.75f);
        }
    }

    private void SpawnEarthShard(Vector3 position, float height, float lifetime)
    {
        GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = "EarthShard";
        shard.layer = gameObject.layer;
        shard.transform.position = position + Vector3.up * (height * 0.34f);
        shard.transform.rotation = Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0f, 360f), Random.Range(-18f, 18f));
        shard.transform.localScale = new Vector3(
            Random.Range(0.08f, 0.18f) * _effectIntensityScale,
            height * Random.Range(0.45f, 0.82f),
            Random.Range(0.08f, 0.22f) * _effectIntensityScale);
        ApplyRenderer(shard, GetEarthMaterial());
        RemoveCollider(shard);
        Destroy(shard, lifetime);
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

        LineRenderer outerArc = CreateLine(root.transform, "ConeOuterArc", lineMaterial, 0.18f, 8);
        LineRenderer innerArc = CreateLine(root.transform, "ConeInnerArc", GetSoftMaterial(), 0.09f, 9);
        LineRenderer left = CreateLine(root.transform, "ConeLeft", boundaryMaterial, 0.09f, 10);
        LineRenderer right = CreateLine(root.transform, "ConeRight", boundaryMaterial, 0.09f, 10);
        LineRenderer centerSlash = CreateLine(root.transform, "ConeCenterSlash", lineMaterial, 0.075f, 11);
        LineRenderer midSlashA = CreateLine(root.transform, "ConeMidSlashA", GetSoftMaterial(), 0.055f, 12);
        LineRenderer midSlashB = CreateLine(root.transform, "ConeMidSlashB", GetSoftMaterial(), 0.055f, 12);

        Vector3 leftDirection = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up) * forward;
        Vector3 rightDirection = Quaternion.AngleAxis(angle * 0.5f, Vector3.up) * forward;
        Vector3 midLeftDirection = Quaternion.AngleAxis(-angle * 0.22f, Vector3.up) * forward;
        Vector3 midRightDirection = Quaternion.AngleAxis(angle * 0.22f, Vector3.up) * forward;
        Vector3 start = origin + Vector3.up * _groundOffset;
        ApplyLine(outerArc, BuildArc(origin, forward, radius, angle, _groundOffset), new Color(0.45f, 0.88f, 1f, 1f), Color.white, 1f, 0.18f);
        ApplyLine(innerArc, BuildArc(origin, forward, radius * 0.58f, angle * 0.72f, _groundOffset + 0.025f), Color.white, new Color(0.68f, 0.96f, 1f, 0.9f), 0.82f, 0.09f);
        ApplyLine(left, new[] { start, origin + leftDirection * radius + Vector3.up * _groundOffset }, Color.white, new Color(0.55f, 0.92f, 1f, 0.86f), 0.88f, 0.09f);
        ApplyLine(right, new[] { start, origin + rightDirection * radius + Vector3.up * _groundOffset }, Color.white, new Color(0.55f, 0.92f, 1f, 0.86f), 0.88f, 0.09f);
        ApplyLine(centerSlash, new[] { start + forward * 0.45f, origin + forward * radius + Vector3.up * (_groundOffset + 0.04f) }, new Color(0.46f, 0.9f, 1f, 0.9f), Color.white, 0.8f, 0.075f);
        ApplyLine(midSlashA, new[] { start + midLeftDirection * 0.55f, origin + midLeftDirection * radius * 0.82f + Vector3.up * _groundOffset }, Color.white, new Color(0.55f, 0.92f, 1f, 0.65f), 0.52f, 0.055f);
        ApplyLine(midSlashB, new[] { start + midRightDirection * 0.55f, origin + midRightDirection * radius * 0.82f + Vector3.up * _groundOffset }, Color.white, new Color(0.55f, 0.92f, 1f, 0.65f), 0.52f, 0.055f);
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

    private void SpawnVerticalStrike(Vector3 center, float radius, Material material, float duration)
    {
        GameObject root = new GameObject("PlayerVerticalStrike");
        root.layer = gameObject.layer;

        float safeRadius = Mathf.Max(0.1f, radius);
        float safeDuration = Mathf.Max(0.05f, duration);
        Vector3 liftedCenter = center + Vector3.up * (safeRadius * 1.25f);

        LineRenderer verticalHalo = CreateLine(root.transform, "VerticalStrikeHalo", material, 0.14f, 11);
        LineRenderer innerHalo = CreateLine(root.transform, "VerticalStrikeInnerHalo", GetSoftMaterial(), 0.08f, 12);
        LineRenderer downLineA = CreateLine(root.transform, "VerticalStrikeDownA", material, 0.11f, 13);
        LineRenderer downLineB = CreateLine(root.transform, "VerticalStrikeDownB", GetSoftMaterial(), 0.075f, 14);
        LineRenderer sparkA = CreateLine(root.transform, "VerticalStrikeSparkA", GetSoftMaterial(), 0.065f, 15);
        LineRenderer sparkB = CreateLine(root.transform, "VerticalStrikeSparkB", material, 0.065f, 16);

        Vector3 facing = ResolveSafePlanarDirection(ResolveAimDirection(center));
        ApplyLine(verticalHalo, BuildVerticalCircle(liftedCenter, safeRadius * 0.72f, facing), material.color, Color.white, 0.8f, 0.14f);
        ApplyLine(innerHalo, BuildVerticalCircle(liftedCenter, safeRadius * 0.44f, facing), Color.white, material.color, 0.6f, 0.08f);
        ApplyLine(
            downLineA,
            new[] { center + Vector3.up * (safeRadius * 2.4f), center + Vector3.up * _groundOffset },
            Color.white,
            material.color,
            0.9f,
            0.11f);
        ApplyLine(
            downLineB,
            new[] { center + Vector3.up * (safeRadius * 1.9f), center + facing * (safeRadius * 0.18f) + Vector3.up * _groundOffset },
            material.color,
            Color.white,
            0.68f,
            0.075f);
        ApplyLine(sparkA, BuildVerticalSpark(liftedCenter, safeRadius * 0.56f, 35f), Color.white, material.color, 0.56f, 0.065f);
        ApplyLine(sparkB, BuildVerticalSpark(liftedCenter, safeRadius * 0.5f, 128f), material.color, Color.white, 0.5f, 0.065f);

        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        PrepareParticleSystem(particles);

        ParticleSystem.MainModule main = particles.main;
        main.duration = safeDuration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.76f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(safeRadius * 0.72f, safeRadius * 1.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f * _effectIntensityScale, 0.3f * _effectIntensityScale);
        main.startColor = new ParticleSystem.MinMaxGradient(material.color, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.RoundToInt(180f * _burstDensityScale);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(70f * _burstDensityScale)) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = safeRadius * 0.45f;
        shape.length = safeRadius * 1.8f;
        root.transform.position = center + Vector3.up * (safeRadius * 0.2f);
        root.transform.rotation = Quaternion.LookRotation(Vector3.down, facing);

        ParticleSystemRenderer rendererComponent = particles.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetSoftMaterial();
        rendererComponent.sortingOrder = 13;

        particles.Play();
        Destroy(root, safeDuration + 0.9f);
    }

    private void SpawnDirectionalParticles(
        string objectName,
        Vector3 position,
        Vector3 forward,
        Material material,
        float duration,
        int burstCount,
        float range,
        float coneAngle = -1f)
    {
        GameObject root = new GameObject(objectName);
        root.layer = gameObject.layer;
        root.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        PrepareParticleSystem(particles);

        ParticleSystem.MainModule main = particles.main;
        main.duration = duration;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 1.08f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(range * 0.68f, range * 1.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.075f * _effectIntensityScale, 0.42f * _effectIntensityScale);
        main.startColor = new ParticleSystem.MinMaxGradient(material.color, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.RoundToInt(burstCount * _burstDensityScale * 3.2f);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, ResolveBurstCount(burstCount)) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = (coneAngle > 0f ? coneAngle : _iceConeAngle) * 0.5f;
        shape.radius = Mathf.Max(0.2f, range * 0.08f);
        shape.length = Mathf.Max(0.5f, range * 0.32f);

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
        LineRenderer flashRing = CreateLine(root.transform, "BurstFlashRing", GetSoftMaterial(), 0.075f, 13);
        LineRenderer echoRing = CreateLine(root.transform, "BurstEchoRing", GetSoftMaterial(), 0.045f, 14);
        float ringRadius = Mathf.Max(0.22f, 0.48f * scale * _effectIntensityScale);
        ApplyLine(flashRing, BuildCircle(position, ringRadius, 0f), startColor, endColor, 0.62f, 0.075f);
        ApplyLine(echoRing, BuildCircle(position, ringRadius * 1.55f, 0.04f), endColor, startColor, 0.34f, 0.045f);

        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        PrepareParticleSystem(particles);

        float visualScale = scale * _effectIntensityScale;
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.46f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.78f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.35f * visualScale, 4.25f * visualScale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f * visualScale, 0.38f * visualScale);
        main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.RoundToInt(140f * _burstDensityScale);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, ResolveBurstCount(58)) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.22f * visualScale;

        ParticleSystemRenderer rendererComponent = particles.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetSoftMaterial();
        rendererComponent.sortingOrder = 12;

        particles.Play();
        Destroy(root, 1.35f);
    }

    private void SpawnIceSpike(Vector3 position, Vector3 forward, float heightScale)
    {
        Vector3 safeForward = ResolveSafePlanarDirection(forward);
        GameObject root = new GameObject("PlayerIceCrystalCluster");
        root.layer = gameObject.layer;
        root.transform.position = position + Vector3.up * (0.28f + heightScale * 0.32f);
        root.transform.rotation = Quaternion.LookRotation(safeForward, Vector3.up);

        int shardCount = 3;
        for (int i = 0; i < shardCount; i++)
        {
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "IceCrystalShard";
            shard.layer = gameObject.layer;
            shard.transform.SetParent(root.transform, false);
            float side = i - 1f;
            shard.transform.localPosition = new Vector3(side * 0.08f * _crystalVisualScale, i == 0 ? 0f : 0.08f, Random.Range(-0.06f, 0.08f));
            shard.transform.localRotation = Quaternion.Euler(
                Random.Range(-22f, 22f),
                Random.Range(-18f, 18f),
                side * Random.Range(14f, 32f));
            shard.transform.localScale = new Vector3(
                0.08f * _crystalVisualScale * Random.Range(0.75f, 1.18f),
                heightScale * _crystalVisualScale * Random.Range(0.72f, 1.08f),
                0.08f * _crystalVisualScale * Random.Range(0.75f, 1.18f));
            ApplyRenderer(shard, GetIceMaterial());
            RemoveCollider(shard);
        }

        LineRenderer glint = CreateLine(root.transform, "IceCrystalGlint", GetSoftMaterial(), 0.035f, 18);
        ApplyLine(
            glint,
            new[]
            {
                root.transform.position + Vector3.up * (heightScale * 0.15f),
                root.transform.position + Vector3.up * (heightScale * _crystalVisualScale * 1.18f)
            },
            Color.white,
            new Color(0.5f, 0.95f, 1f, 0.72f),
            0.72f,
            0.035f);
        Destroy(root, 1.05f);
    }

    private void SpawnIcePillar(Vector3 position, float height, float width, float lifetime)
    {
        GameObject root = new GameObject("PlayerWinterCrystalPillar");
        root.layer = gameObject.layer;
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(Random.Range(-4f, 4f), Random.Range(0f, 360f), Random.Range(-4f, 4f));

        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "WinterCrystalCore";
        pillar.layer = gameObject.layer;
        pillar.transform.SetParent(root.transform, false);
        pillar.transform.localPosition = Vector3.up * (height * 0.5f);
        pillar.transform.localScale = new Vector3(width * _crystalVisualScale, height * 0.5f, width * _crystalVisualScale);
        ApplyRenderer(pillar, GetIceMaterial());
        RemoveCollider(pillar);

        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f + Random.Range(-16f, 16f);
            Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = "WinterCrystalFacet";
            shard.layer = gameObject.layer;
            shard.transform.SetParent(root.transform, false);
            shard.transform.localPosition = direction * width * Random.Range(0.55f, 1.15f) + Vector3.up * height * Random.Range(0.35f, 0.78f);
            shard.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(Random.Range(-22f, 22f), 0f, Random.Range(-18f, 18f));
            shard.transform.localScale = new Vector3(width * 0.38f, height * Random.Range(0.24f, 0.42f), width * 0.18f);
            ApplyRenderer(shard, GetIceMaterial());
            RemoveCollider(shard);
        }

        LineRenderer halo = CreateLine(root.transform, "WinterPillarHalo", GetSoftMaterial(), 0.045f, 17);
        ApplyLine(halo, BuildCircle(position + Vector3.up * 0.04f, width * 2.8f, 0f), Color.white, GetIceMaterial().color, 0.42f, 0.045f);
        Destroy(root, lifetime);
    }

    private void ConfigureTrailParticles(ParticleSystem particles, Color startColor, Color endColor, float lifetime, float size)
    {
        PrepareParticleSystem(particles);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f * _effectIntensityScale, size * _effectIntensityScale);
        main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.RoundToInt(160f * _burstDensityScale);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 96f * _burstDensityScale;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.13f;

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
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.22f, 0.72f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f * _effectIntensityScale, 0.28f * _effectIntensityScale);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.maxParticles = Mathf.RoundToInt(240f * _burstDensityScale);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 86f * _burstDensityScale;

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
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

    private Vector3[] BuildRadialSegment(Vector3 center, float angle, float innerRadius, float outerRadius, float heightOffset)
    {
        Vector3 direction = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad));
        return new[]
        {
            center + direction * innerRadius + Vector3.up * heightOffset,
            center + direction * outerRadius + Vector3.up * heightOffset
        };
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
        line.widthMultiplier = Mathf.Max(0.001f, width * _lineWidthScale);
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
        line.widthMultiplier = Mathf.Max(0.001f, width * _lineWidthScale);
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

    private short ResolveBurstCount(float baseCount)
    {
        return (short)Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Max(1f, baseCount) * _burstDensityScale),
            1,
            short.MaxValue);
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
