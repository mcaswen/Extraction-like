using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class MudTidalAberrationVfx : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform _target;
    [SerializeField] private string _targetObjectName = "Player";
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.9f, 0f);
    [SerializeField] private LayerMask _hitMask = ~0;

    [Header("Tentacle Contact")]
    [SerializeField] private bool _enableTentacleContactDamage = true;
    [SerializeField] private float _tentacleContactRange = 3f;
    [SerializeField] private float _tentacleContactInterval = 1f;
    [SerializeField] private float _tentacleContactDuration = 3f;
    [SerializeField] private float _tentacleContactDamage = 20f;
    [SerializeField] private float _silenceDuration = 1.5f;
    [SerializeField] private float _tentacleOriginHeight = 0.7f;
    [SerializeField] private float _tentacleSpreadRadius = 0.85f;

    [Header("High Pressure Jet")]
    [SerializeField] private bool _autoWaterJetWhenPlayerDetected = true;
    [SerializeField] private float _detectionRange = 14f;
    [SerializeField] private float _waterJetInterval = 8f;
    [SerializeField] private float _waterJetDuration = 0.42f;
    [SerializeField] private float _waterJetDamage = 30f;
    [SerializeField] private float _waterJetMaxDistance = 11f;
    [SerializeField] private float _waterJetRadius = 0.35f;
    [SerializeField] private float _waterJetKnockbackStrength = 5.2f;
    [SerializeField] private float _knockbackMoveSpeedMultiplier = 0.5f;
    [SerializeField] private float _knockbackSlowDuration = 1f;
    [SerializeField] private Vector3 _waterJetOriginLocalOffset = new Vector3(0f, 1.15f, 0.5f);
    [SerializeField] private bool _faceTargetWhileAttacking = true;
    [SerializeField] private bool _allowKeyboardPreview = true;
    [SerializeField] private KeyCode _previewTentacleKey = KeyCode.U;
    [SerializeField] private KeyCode _previewWaterJetKey = KeyCode.I;

    [Header("Visual")]
    [SerializeField] private int _pathPointCount = 20;
    [SerializeField] private Color _tentacleColor = new Color(0.055f, 0.16f, 0.14f, 0.96f);
    [SerializeField] private Color _tentacleSpikeColor = new Color(0.68f, 1f, 0.88f, 0.75f);
    [SerializeField] private Color _waterCoreColor = new Color(0.82f, 1f, 1f, 1f);
    [SerializeField] private Color _waterEdgeColor = new Color(0.22f, 0.78f, 1f, 0.8f);
    [SerializeField] private Color _impactFoamColor = new Color(0.92f, 1f, 1f, 0.9f);

    private const int MinimumPathPoints = 6;
    private const int TargetRefreshIntervalFrames = 20;
    private static readonly RaycastHit[] s_waterJetHits = new RaycastHit[16];
    private static Texture2D s_softParticleTexture;

    private readonly List<TentacleArm> _idleTentacles = new List<TentacleArm>();
    private readonly List<SimpleMagicRangedAttack> _silencedSimpleMagicScripts = new List<SimpleMagicRangedAttack>();
    private readonly List<bool> _silencedSimpleMagicEnabledStates = new List<bool>();

    private Transform _tentacleOriginOverride;
    private Transform _waterJetOriginOverride;
    private Transform _targetCandidate;
    private Coroutine _tentacleRoutine;
    private Coroutine _waterJetRoutine;
    private Coroutine _sceneLocalSilenceRoutine;
    private Material _tentacleMaterial;
    private Material _tentacleSpikeMaterial;
    private Material _waterMaterial;
    private Material _foamMaterial;
    private float _tentacleContactTimer;
    private float _waterJetTimer;
    private float _noiseSeed;
    private int _targetRefreshFrame;
    private bool _suppressGameplayEffects;

    private void Awake()
    {
        _pathPointCount = Mathf.Max(MinimumPathPoints, _pathPointCount);
        _tentacleContactTimer = Mathf.Max(0.05f, _tentacleContactInterval);
        _waterJetTimer = Mathf.Max(0.2f, _waterJetInterval);
        _noiseSeed = Random.Range(0f, 100f);
        EnsureRuntimeMaterials();
        EnsureIdleTentacles();
    }

    private void OnDisable()
    {
        if (_tentacleRoutine != null)
        {
            StopCoroutine(_tentacleRoutine);
            _tentacleRoutine = null;
        }

        if (_waterJetRoutine != null)
        {
            StopCoroutine(_waterJetRoutine);
            _waterJetRoutine = null;
        }

        RestoreSceneLocalSilence();

        for (int i = 0; i < _idleTentacles.Count; i++)
        {
            _idleTentacles[i].Destroy();
        }

        _idleTentacles.Clear();

        DestroyRuntimeMaterial(ref _tentacleMaterial);
        DestroyRuntimeMaterial(ref _tentacleSpikeMaterial);
        DestroyRuntimeMaterial(ref _waterMaterial);
        DestroyRuntimeMaterial(ref _foamMaterial);
    }

    private void Update()
    {
        ResolveTargetIfNeeded();
        UpdateIdleTentacles();

        if (_targetCandidate == null)
        {
            return;
        }

        if (_faceTargetWhileAttacking && (_tentacleRoutine != null || _waterJetRoutine != null))
        {
            FaceTarget();
        }

        if (_allowKeyboardPreview)
        {
            if (Input.GetKeyDown(_previewTentacleKey))
            {
                TriggerTentacleContact();
            }

            if (Input.GetKeyDown(_previewWaterJetKey))
            {
                TriggerWaterJet();
            }
        }

        TickTentacleContact();
        TickWaterJet();
    }

    public void TriggerTentacleContact()
    {
        if (_tentacleRoutine != null || _targetCandidate == null)
        {
            return;
        }

        _tentacleContactTimer = Mathf.Max(0.05f, _tentacleContactInterval);
        _tentacleRoutine = StartCoroutine(PlayTentacleContact());
    }

    public void TriggerWaterJet()
    {
        if (_waterJetRoutine != null || _targetCandidate == null)
        {
            return;
        }

        _waterJetTimer = Mathf.Max(0.2f, _waterJetInterval);
        _waterJetRoutine = StartCoroutine(PlayWaterJet());
    }

    public void ConfigureAsEnemyDrivenVisual(
        Transform target,
        Transform tentacleOrigin,
        Transform waterJetOrigin,
        float tentacleDuration,
        float waterJetDuration)
    {
        _target = target;
        _targetCandidate = target;
        _tentacleOriginOverride = tentacleOrigin;
        _waterJetOriginOverride = waterJetOrigin;
        _tentacleContactDuration = Mathf.Max(0.05f, tentacleDuration);
        _waterJetDuration = Mathf.Max(0.05f, waterJetDuration);
        _enableTentacleContactDamage = false;
        _autoWaterJetWhenPlayerDetected = false;
        _allowKeyboardPreview = false;
        _suppressGameplayEffects = true;
    }

    public void PlayTentacleContactVisual(Transform target)
    {
        _target = target;
        _targetCandidate = target;
        TriggerTentacleContact();
    }

    public void PlayWaterJetVisual(Transform target)
    {
        _target = target;
        _targetCandidate = target;
        TriggerWaterJet();
    }

    private void TickTentacleContact()
    {
        if (!_enableTentacleContactDamage || _tentacleRoutine != null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, _targetCandidate.position);
        if (distance > _tentacleContactRange)
        {
            _tentacleContactTimer = Mathf.Min(_tentacleContactTimer, 0.2f);
            return;
        }

        _tentacleContactTimer -= Time.deltaTime;
        if (_tentacleContactTimer <= 0f)
        {
            TriggerTentacleContact();
        }
    }

    private void TickWaterJet()
    {
        if (!_autoWaterJetWhenPlayerDetected || _waterJetRoutine != null)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, _targetCandidate.position);
        if (distance > _detectionRange)
        {
            _waterJetTimer = Mathf.Min(_waterJetTimer, 0.5f);
            return;
        }

        _waterJetTimer -= Time.deltaTime;
        if (_waterJetTimer <= 0f)
        {
            TriggerWaterJet();
        }
    }

    private IEnumerator PlayTentacleContact()
    {
        FaceTarget();
        TentacleArm attackTentacle = new TentacleArm(this, "MudContactTentacleVfx", 0.24f, 0.08f, 10);
        bool initialHitApplied = false;
        float tickTimer = 0f;
        float duration = Mathf.Max(0.05f, _tentacleContactDuration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / duration);
            Vector3 origin = ResolveTentacleOrigin(0);
            Vector3 targetPoint = ResolveTargetPoint();
            Vector3[] path = BuildTentaclePath(origin, targetPoint, normalized * 5f, 0.35f);
            attackTentacle.Update(path, 1f - Mathf.SmoothStep(0.88f, 1f, normalized), normalized);

            if (!initialHitApplied)
            {
                initialHitApplied = true;
                ApplyTentacleHit(targetPoint, _tentacleContactDamage);
                SpawnFoamBurst(targetPoint, 30, 0.08f, 1.2f);
            }

            tickTimer += Time.deltaTime;
            while (tickTimer >= Mathf.Max(0.05f, _tentacleContactInterval))
            {
                tickTimer -= Mathf.Max(0.05f, _tentacleContactInterval);
                ApplyTentacleHit(targetPoint, Mathf.Max(0f, _tentacleContactDamage) * 0.25f);
            }

            yield return null;
        }

        attackTentacle.Destroy();
        _tentacleRoutine = null;
    }

    private IEnumerator PlayWaterJet()
    {
        FaceTarget();
        GameObject jetRoot = new GameObject("MudHighPressureWaterJetVfx");
        jetRoot.layer = gameObject.layer;

        LineRenderer outerLine = CreateLine(jetRoot.transform, "WaterJetOuter", _waterMaterial, 0.42f, 12);
        LineRenderer coreLine = CreateLine(jetRoot.transform, "WaterJetCore", _waterMaterial, 0.18f, 13);
        LineRenderer sprayLine = CreateLine(jetRoot.transform, "WaterJetSprayEdge", _waterMaterial, 0.08f, 11);
        ParticleSystem originSpray = CreateWaterParticles(jetRoot.transform, "WaterJetOriginSpray", 0.08f, 0.34f, 0.2f);
        ParticleSystem impactSpray = CreateWaterParticles(jetRoot.transform, "WaterJetImpactBurst", 0.1f, 0.46f, 0.28f);

        bool damageApplied = false;
        float duration = Mathf.Max(0.05f, _waterJetDuration);
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / duration);
            Vector3 origin = ResolveWaterJetOrigin();
            Vector3 target = ResolveTargetPoint();
            Vector3 direction = target - origin;
            float distance = Mathf.Min(_waterJetMaxDistance, Mathf.Max(0.2f, direction.magnitude));
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = transform.forward;
            }

            direction.Normalize();
            Vector3 endpoint = origin + direction * distance;
            Vector3[] outerPath = BuildWaterJetPath(origin, endpoint, normalized, 0.08f);
            Vector3[] corePath = BuildWaterJetPath(origin, endpoint, normalized, 0.018f);
            Vector3[] sprayPath = BuildWaterJetPath(origin, endpoint, normalized + 0.35f, -0.12f);

            ApplyLine(outerLine, outerPath, _waterEdgeColor, _impactFoamColor, 0.9f, 0.42f);
            ApplyLine(coreLine, corePath, _waterCoreColor, _waterCoreColor, 1f, 0.18f);
            ApplyLine(sprayLine, sprayPath, _impactFoamColor, _waterEdgeColor, 0.46f, 0.08f);
            originSpray.transform.position = origin;
            impactSpray.transform.position = endpoint;

            if (!damageApplied && normalized >= 0.18f)
            {
                damageApplied = true;
                ApplyWaterJetHit(origin, direction, distance);
                SpawnFoamBurst(endpoint, 42, 0.1f, 1.8f);
            }

            yield return null;
        }

        Destroy(jetRoot, 0.55f);
        _waterJetRoutine = null;
    }

    private void ApplyTentacleHit(Vector3 hitPoint, float damage)
    {
        if (_suppressGameplayEffects)
        {
            return;
        }

        if (_targetCandidate == null)
        {
            return;
        }

        Vector3 hitDirection = _targetCandidate.position - transform.position;
        if (hitDirection.sqrMagnitude <= 0.0001f)
        {
            hitDirection = transform.forward;
        }

        TryApplyDamage(_targetCandidate, damage, hitPoint, hitDirection.normalized);
        PlayerShootingController shootingController = _targetCandidate.GetComponent<PlayerShootingController>();
        if (shootingController == null)
        {
            shootingController = _targetCandidate.GetComponentInChildren<PlayerShootingController>();
        }

        if (shootingController != null)
        {
            shootingController.ApplySilence(_silenceDuration);
        }

        ApplySceneLocalSilence(_targetCandidate, _silenceDuration);
    }

    private void ApplySceneLocalSilence(Transform target, float duration)
    {
        if (target == null || duration <= 0f)
        {
            return;
        }

        SimpleMagicRangedAttack[] simpleMagicScripts = target.GetComponentsInChildren<SimpleMagicRangedAttack>(true);
        if (simpleMagicScripts.Length == 0)
        {
            return;
        }

        RestoreSceneLocalSilence();
        for (int i = 0; i < simpleMagicScripts.Length; i++)
        {
            SimpleMagicRangedAttack simpleMagic = simpleMagicScripts[i];
            if (simpleMagic == null)
            {
                continue;
            }

            _silencedSimpleMagicScripts.Add(simpleMagic);
            _silencedSimpleMagicEnabledStates.Add(simpleMagic.enabled);
            simpleMagic.enabled = false;
        }

        if (_silencedSimpleMagicScripts.Count > 0)
        {
            _sceneLocalSilenceRoutine = StartCoroutine(SceneLocalSilenceCountdown(duration));
        }
    }

    private IEnumerator SceneLocalSilenceCountdown(float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        _sceneLocalSilenceRoutine = null;
        RestoreSceneLocalSilenceState();
    }

    private void RestoreSceneLocalSilence()
    {
        if (_sceneLocalSilenceRoutine != null)
        {
            StopCoroutine(_sceneLocalSilenceRoutine);
            _sceneLocalSilenceRoutine = null;
        }

        RestoreSceneLocalSilenceState();
    }

    private void RestoreSceneLocalSilenceState()
    {
        for (int i = 0; i < _silencedSimpleMagicScripts.Count; i++)
        {
            SimpleMagicRangedAttack simpleMagic = _silencedSimpleMagicScripts[i];
            if (simpleMagic != null)
            {
                simpleMagic.enabled = _silencedSimpleMagicEnabledStates[i];
            }
        }

        _silencedSimpleMagicScripts.Clear();
        _silencedSimpleMagicEnabledStates.Clear();
    }

    private void ApplyWaterJetHit(Vector3 origin, Vector3 direction, float distance)
    {
        if (_suppressGameplayEffects)
        {
            return;
        }

        bool hitApplied = false;
        int hitCount = Physics.SphereCastNonAlloc(origin, Mathf.Max(0.02f, _waterJetRadius), direction, s_waterJetHits, distance, _hitMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = s_waterJetHits[i];
            s_waterJetHits[i] = default;
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (CombatDamageUtility.TryGetDamageReceiver(hit.collider, out ICombatDamageReceiver receiver))
            {
                CombatDamageUtility.ApplyDamageTo(receiver, _waterJetDamage, hit.point, direction, gameObject);
                hitApplied = true;
                break;
            }
        }

        if (!hitApplied && _targetCandidate != null)
        {
            TryApplyDamage(_targetCandidate, _waterJetDamage, ResolveTargetPoint(), direction);
        }

        ApplyWaterJetKnockback(direction);
    }

    private void TryApplyDamage(Transform target, float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (target == null || damage <= 0f)
        {
            return;
        }

        if (CombatDamageUtility.TryGetDamageReceiver(target, out ICombatDamageReceiver receiver))
        {
            CombatDamageUtility.ApplyDamageTo(receiver, damage, hitPoint, hitDirection, gameObject);
            return;
        }

        CombatDamageUtility.ApplyDamageTo(target, damage, hitPoint, hitDirection, gameObject);
    }

    private void ApplyWaterJetKnockback(Vector3 direction)
    {
        if (_suppressGameplayEffects)
        {
            return;
        }

        if (_targetCandidate == null)
        {
            return;
        }

        PlayerMovementController movementController = _targetCandidate.GetComponent<PlayerMovementController>();
        if (movementController == null)
        {
            movementController = _targetCandidate.GetComponentInChildren<PlayerMovementController>();
        }

        if (movementController != null)
        {
            movementController.ApplyExternalImpulse(direction, _waterJetKnockbackStrength);
            movementController.ApplyMoveSpeedDebuff(_knockbackMoveSpeedMultiplier, _knockbackSlowDuration);
            return;
        }

        CharacterController characterController = _targetCandidate.GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = _targetCandidate.GetComponentInChildren<CharacterController>();
        }

        Vector3 planarDirection = direction;
        planarDirection.y = 0f;
        if (characterController != null && characterController.enabled && planarDirection.sqrMagnitude > 0.0001f)
        {
            characterController.Move(planarDirection.normalized * Mathf.Max(0f, _waterJetKnockbackStrength) * 0.18f);
        }
    }

    private void ResolveTargetIfNeeded()
    {
        if (_target != null)
        {
            _targetCandidate = _target;
            return;
        }

        _targetRefreshFrame--;
        if (_targetCandidate != null && _targetRefreshFrame > 0)
        {
            return;
        }

        _targetRefreshFrame = TargetRefreshIntervalFrames;

        if (PlayerTargetResolver.TryGetCurrentPlayerTransform(transform, out Transform currentPlayer))
        {
            _targetCandidate = currentPlayer;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_targetObjectName))
        {
            GameObject namedTarget = GameObject.Find(_targetObjectName);
            if (namedTarget != null)
            {
                _targetCandidate = namedTarget.transform;
                return;
            }
        }

        _targetCandidate = null;
    }

    private void FaceTarget()
    {
        if (_targetCandidate == null)
        {
            return;
        }

        Vector3 toTarget = _targetCandidate.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(toTarget.normalized, Vector3.up),
            Time.deltaTime * 8f);
    }

    private Vector3 ResolveTargetPoint()
    {
        if (_targetCandidate == null)
        {
            return transform.position + transform.forward * 4f + _targetOffset;
        }

        return _targetCandidate.position + _targetOffset;
    }

    private Vector3 ResolveTentacleOrigin(int index)
    {
        if (_tentacleOriginOverride != null)
        {
            return _tentacleOriginOverride.position;
        }

        float angle = (index / 6f) * Mathf.PI * 2f + Time.time * 0.25f;
        Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _tentacleSpreadRadius;
        return transform.position + radial + Vector3.up * _tentacleOriginHeight;
    }

    private Vector3 ResolveWaterJetOrigin()
    {
        if (_waterJetOriginOverride != null)
        {
            return _waterJetOriginOverride.position;
        }

        return transform.TransformPoint(_waterJetOriginLocalOffset);
    }

    private void EnsureIdleTentacles()
    {
        if (_idleTentacles.Count > 0)
        {
            return;
        }

        for (int i = 0; i < 6; i++)
        {
            _idleTentacles.Add(new TentacleArm(this, "MudIdleTentacleVfx", 0.14f, 0.045f, 2 + i));
        }
    }

    private void UpdateIdleTentacles()
    {
        EnsureIdleTentacles();
        for (int i = 0; i < _idleTentacles.Count; i++)
        {
            Vector3 origin = ResolveTentacleOrigin(i);
            float angle = (i / (float)_idleTentacles.Count) * Mathf.PI * 2f + Mathf.Sin(Time.time * 0.8f + i) * 0.18f;
            Vector3 endpoint = transform.position +
                               new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (1.1f + Mathf.Sin(Time.time + i) * 0.22f) +
                               Vector3.up * (0.24f + Mathf.Sin(Time.time * 1.3f + i) * 0.18f);
            Vector3[] path = BuildTentaclePath(origin, endpoint, Time.time * 0.9f + i, 0.42f);
            _idleTentacles[i].Update(path, 0.72f, Time.time + i);
        }
    }

    private Vector3[] BuildTentaclePath(Vector3 origin, Vector3 endpoint, float phase, float amplitude)
    {
        Vector3 delta = endpoint - origin;
        Vector3 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = transform.right;
        }

        right.Normalize();
        Vector3 up = Vector3.Cross(direction, right).normalized;
        Vector3[] points = new Vector3[_pathPointCount];
        for (int i = 0; i < points.Length; i++)
        {
            float u = i / (float)(points.Length - 1);
            float envelope = Mathf.Sin(u * Mathf.PI);
            float wave = Mathf.Sin(u * Mathf.PI * 2.8f + phase + _noiseSeed) * amplitude * 0.22f;
            float curl = Mathf.Cos(u * Mathf.PI * 3.5f + phase * 0.75f) * amplitude * 0.12f;
            points[i] = Vector3.Lerp(origin, endpoint, u) + right * wave * envelope + up * curl * envelope;
        }

        return points;
    }

    private Vector3[] BuildWaterJetPath(Vector3 origin, Vector3 endpoint, float phase, float amplitude)
    {
        Vector3 delta = endpoint - origin;
        Vector3 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = transform.right;
        }

        right.Normalize();
        Vector3 up = Vector3.Cross(direction, right).normalized;
        Vector3[] points = new Vector3[_pathPointCount];
        for (int i = 0; i < points.Length; i++)
        {
            float u = i / (float)(points.Length - 1);
            float envelope = Mathf.Sin(u * Mathf.PI);
            float pressureJitter = Mathf.Sin(Time.time * 34f + u * 28f + phase) * amplitude;
            points[i] = Vector3.Lerp(origin, endpoint, u) + (right * pressureJitter + up * pressureJitter * 0.3f) * envelope;
        }

        return points;
    }

    private LineRenderer CreateLine(Transform parent, string objectName, Material material, float width, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.layer = gameObject.layer;
        lineObject.transform.SetParent(parent, false);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.numCapVertices = 6;
        lineRenderer.numCornerVertices = 6;
        lineRenderer.widthMultiplier = width;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.sharedMaterial = material;
        lineRenderer.sortingOrder = sortingOrder;
        return lineRenderer;
    }

    private void ApplyLine(LineRenderer lineRenderer, Vector3[] path, Color startColor, Color endColor, float alpha, float width)
    {
        startColor.a *= alpha;
        endColor.a *= alpha;
        lineRenderer.positionCount = path.Length;
        lineRenderer.SetPositions(path);
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        lineRenderer.widthMultiplier = Mathf.Max(0.001f, width);
        lineRenderer.enabled = alpha > 0.02f;
    }

    private void SpawnFoamBurst(Vector3 position, int count, float size, float speed)
    {
        GameObject burstObject = new GameObject("MudTidalFoamBurst");
        burstObject.layer = gameObject.layer;
        burstObject.transform.position = position;

        ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();
        ConfigureParticleSystem(particles, size, 0.5f, speed, 0f);
        particles.Emit(count);
        Destroy(burstObject, 1f);
    }

    private ParticleSystem CreateWaterParticles(Transform parent, string objectName, float size, float lifetime, float radius)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.layer = gameObject.layer;
        particleObject.transform.SetParent(parent, false);

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ConfigureParticleSystem(particles, size, lifetime, 1.2f, 34f);
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
        return particles;
    }

    private void ConfigureParticleSystem(ParticleSystem particles, float size, float lifetime, float speed, float rate)
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.loop = rate > 0f;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size * 1.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(_waterCoreColor, _impactFoamColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 160;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(_waterCoreColor, 0f),
                new GradientColorKey(_waterEdgeColor, 0.45f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.15f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer rendererComponent = particles.GetComponent<ParticleSystemRenderer>();
        rendererComponent.sharedMaterial = _foamMaterial;
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sortingOrder = 14;
        rendererComponent.shadowCastingMode = ShadowCastingMode.Off;
        rendererComponent.receiveShadows = false;

        if (rate > 0f)
        {
            particles.Play();
        }
    }

    private void EnsureRuntimeMaterials()
    {
        if (_tentacleMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        _tentacleMaterial = CreateMaterial("Runtime Mud Tentacle", shader, _tentacleColor, null);
        _tentacleSpikeMaterial = CreateMaterial("Runtime Mud Tentacle Spikes", shader, _tentacleSpikeColor, null);
        _waterMaterial = CreateMaterial("Runtime Mud High Pressure Water", shader, _waterCoreColor, null);
        _foamMaterial = CreateMaterial("Runtime Mud Foam Particles", shader, _impactFoamColor, GetSoftParticleTexture());
    }

    private static Material CreateMaterial(string name, Shader shader, Color color, Texture texture)
    {
        Material material = new Material(shader)
        {
            name = name,
            color = color,
            renderQueue = (int)RenderQueue.Transparent
        };

        if (texture != null)
        {
            material.mainTexture = texture;
        }

        return material;
    }

    private static Texture2D GetSoftParticleTexture()
    {
        if (s_softParticleTexture != null)
        {
            return s_softParticleTexture;
        }

        const int size = 64;
        s_softParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Mud Foam Soft Circle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(u * u + v * v);
                float alpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance));
                s_softParticleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        s_softParticleTexture.Apply();
        return s_softParticleTexture;
    }

    private static void DestroyRuntimeMaterial(ref Material material)
    {
        if (material == null)
        {
            return;
        }

        Destroy(material);
        material = null;
    }

    private sealed class TentacleArm
    {
        private readonly MudTidalAberrationVfx _owner;
        private readonly GameObject _rootObject;
        private readonly LineRenderer _bodyLine;
        private readonly LineRenderer _rimLine;
        private readonly Transform[] _spikes;

        public TentacleArm(MudTidalAberrationVfx owner, string rootName, float bodyWidth, float rimWidth, int sortingOrder)
        {
            _owner = owner;
            _rootObject = new GameObject(rootName);
            _rootObject.layer = owner.gameObject.layer;

            _bodyLine = owner.CreateLine(_rootObject.transform, "TentacleBody", owner._tentacleMaterial, bodyWidth, sortingOrder);
            _rimLine = owner.CreateLine(_rootObject.transform, "TentacleRim", owner._tentacleSpikeMaterial, rimWidth, sortingOrder + 1);
            _spikes = new Transform[7];
            for (int i = 0; i < _spikes.Length; i++)
            {
                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                spike.name = "TentacleSpike";
                spike.layer = owner.gameObject.layer;
                spike.transform.SetParent(_rootObject.transform, false);
                Collider collider = spike.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                Renderer renderer = spike.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = owner._tentacleSpikeMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                _spikes[i] = spike.transform;
            }
        }

        public void Update(Vector3[] path, float alpha, float phase)
        {
            if (path == null || path.Length < 2)
            {
                return;
            }

            _owner.ApplyLine(_bodyLine, path, _owner._tentacleColor, _owner._tentacleColor, alpha, _bodyLine.widthMultiplier);
            _owner.ApplyLine(_rimLine, OffsetPath(path, Mathf.Sin(phase * 5f) * 0.055f), _owner._tentacleSpikeColor, _owner._tentacleSpikeColor, alpha * 0.8f, _rimLine.widthMultiplier);
            UpdateSpikes(path, alpha);
        }

        public void Destroy()
        {
            if (_rootObject != null)
            {
                Object.Destroy(_rootObject);
            }
        }

        private void UpdateSpikes(Vector3[] path, float alpha)
        {
            for (int i = 0; i < _spikes.Length; i++)
            {
                float u = Mathf.Lerp(0.18f, 0.86f, i / (float)(_spikes.Length - 1));
                SamplePath(path, u, out Vector3 position, out Vector3 tangent);
                Vector3 right = Vector3.Cross(Vector3.up, tangent);
                if (right.sqrMagnitude <= 0.0001f)
                {
                    right = _owner.transform.right;
                }

                right.Normalize();
                Transform spike = _spikes[i];
                spike.gameObject.SetActive(alpha > 0.05f);
                spike.position = position + right * (i % 2 == 0 ? 0.12f : -0.12f);
                Vector3 spikeDirection = (right + tangent * 0.18f).normalized;
                spike.rotation = Quaternion.FromToRotation(Vector3.up, spikeDirection);
                float size = Mathf.Lerp(0.12f, 0.07f, u) * Mathf.Clamp01(alpha);
                spike.localScale = new Vector3(size * 0.55f, size * 1.55f, size * 0.55f);
            }
        }

        private Vector3[] OffsetPath(Vector3[] path, float offset)
        {
            Vector3[] result = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 tangent = i < path.Length - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1];
                if (tangent.sqrMagnitude <= 0.0001f)
                {
                    tangent = Vector3.forward;
                }

                tangent.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, tangent);
                if (right.sqrMagnitude <= 0.0001f)
                {
                    right = _owner.transform.right;
                }

                right.Normalize();
                float u = i / (float)(path.Length - 1);
                result[i] = path[i] + right * offset * Mathf.Sin(u * Mathf.PI);
            }

            return result;
        }

        private static void SamplePath(Vector3[] path, float normalized, out Vector3 position, out Vector3 tangent)
        {
            normalized = Mathf.Clamp01(normalized);
            float scaled = normalized * (path.Length - 1);
            int index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, path.Length - 2);
            float t = scaled - index;
            position = Vector3.Lerp(path[index], path[index + 1], t);
            tangent = path[index + 1] - path[index];
            if (tangent.sqrMagnitude <= 0.0001f)
            {
                tangent = Vector3.forward;
            }
            else
            {
                tangent.Normalize();
            }
        }
    }
}
