using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class RobotAnchorBeamVfx : MonoBehaviour
{
    private enum BeamState
    {
        Dormant,
        Locking,
        Firing,
        Cooldown
    }

    [Header("Target")]
    [SerializeField] private Transform _target;
    [SerializeField] private string _targetObjectName = "Player";
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.9f, 0f);

    [Header("Origin")]
    [SerializeField] private Transform _beamOrigin;
    [SerializeField] private Vector3 _originLocalOffset = new Vector3(0f, 1.45f, 0.55f);
    [SerializeField] private bool _faceTargetWhileActive = true;

    [Header("Activation")]
    [SerializeField] private bool _autoActivateOnPlayerRange = true;
    [SerializeField] private float _activationRange = 12f;
    [SerializeField] private float _initialDelay = 0.8f;
    [SerializeField] private float _lockDuration = 0.8f;
    [SerializeField] private float _beamDuration = 3f;
    [SerializeField] private float _cooldownDuration = 3f;
    [SerializeField] private bool _allowKeyboardPreview = true;
    [SerializeField] private KeyCode _previewActivateKey = KeyCode.L;

    [Header("Damage")]
    [SerializeField] private LayerMask _damageMask = ~0;
    [SerializeField] private float _beamRadius = 0.32f;
    [SerializeField] private float _beamDamagePerSecond = 22f;
    [SerializeField] private float _damageTickInterval = 0.12f;

    [Header("Visual")]
    [SerializeField] private int _beamPointCount = 20;
    [SerializeField] private Color _outerBeamColor = new Color(0.08f, 0.28f, 1f, 0.68f);
    [SerializeField] private Color _coreBeamColor = new Color(0.78f, 1f, 1f, 1f);
    [SerializeField] private Color _edgeBeamColor = new Color(0.12f, 0.86f, 1f, 0.85f);
    [SerializeField] private Color _runeColor = new Color(0.25f, 0.92f, 1f, 0.82f);

    private const int MinimumBeamPoints = 6;
    private const int CircleSegments = 56;
    private const float TargetRefreshInterval = 0.35f;
    private const float FacingTurnSpeed = 7.5f;

    private static readonly RaycastHit[] s_damageHits = new RaycastHit[24];
    private static Texture2D s_softCircleTexture;

    private readonly HashSet<ICombatDamageReceiver> _damageReceiversThisTick = new HashSet<ICombatDamageReceiver>();

    private BeamState _state;
    private Transform _targetCandidate;
    private float _stateTimer;
    private float _activationTimer;
    private float _targetRefreshTimer;
    private float _damageTickTimer;
    private float _noiseSeed;

    private GameObject _rigRoot;
    private Transform _impactRoot;

    private Material _beamMaterial;
    private Material _runeMaterial;
    private Material _particleMaterial;

    private LineRenderer _lockLine;
    private LineRenderer _outerBeamLine;
    private LineRenderer _edgeBeamLine;
    private LineRenderer _coreBeamLine;
    private LineRenderer _wispLineA;
    private LineRenderer _wispLineB;
    private LineRenderer _baseRingLine;
    private LineRenderer _topRingLine;
    private LineRenderer _originRingLine;
    private LineRenderer _impactRingLine;
    private LineRenderer _crossbarLine;
    private LineRenderer _runeTriangleLine;

    private ParticleSystem _originParticles;
    private ParticleSystem _impactParticles;
    private ParticleSystem _driftParticles;

    private void Awake()
    {
        _beamPointCount = Mathf.Max(MinimumBeamPoints, _beamPointCount);
        _activationTimer = Mathf.Max(0f, _initialDelay);
        _noiseSeed = Random.Range(0f, 100f);

        EnsureRuntimeMaterials();
        EnsureVisuals();
        HideBeamImmediate();
    }

    private void OnDisable()
    {
        if (_rigRoot != null)
        {
            Destroy(_rigRoot);
            _rigRoot = null;
        }

        DestroyRuntimeMaterial(ref _beamMaterial);
        DestroyRuntimeMaterial(ref _runeMaterial);
        DestroyRuntimeMaterial(ref _particleMaterial);
    }

    private void Update()
    {
        ResolveTargetIfNeeded();
        EnsureVisuals();

        if (_targetCandidate == null)
        {
            HideBeamImmediate();
            UpdateGlyphVisual(0.35f);
            return;
        }

        if (_allowKeyboardPreview && Input.GetKeyDown(_previewActivateKey))
        {
            BeginLocking();
        }

        switch (_state)
        {
            case BeamState.Dormant:
                TickDormant();
                break;
            case BeamState.Locking:
                TickLocking();
                break;
            case BeamState.Firing:
                TickFiring();
                break;
            case BeamState.Cooldown:
                TickCooldown();
                break;
        }
    }

    private void TickDormant()
    {
        HideBeamImmediate();
        UpdateGlyphVisual(0.48f + Mathf.Sin(Time.time * 2.2f + _noiseSeed) * 0.08f);

        if (!_autoActivateOnPlayerRange)
        {
            return;
        }

        _activationTimer -= Time.deltaTime;
        if (_activationTimer > 0f)
        {
            return;
        }

        if (Vector3.Distance(transform.position, _targetCandidate.position) <= _activationRange)
        {
            BeginLocking();
        }
    }

    private void TickLocking()
    {
        _stateTimer += Time.deltaTime;
        FaceTargetIfNeeded();

        float normalized = Mathf.Clamp01(_stateTimer / Mathf.Max(0.05f, _lockDuration));
        UpdateGlyphVisual(0.75f + Mathf.Sin(Time.time * 10f) * 0.1f);
        UpdateLockLine(normalized);

        if (_stateTimer >= Mathf.Max(0.05f, _lockDuration))
        {
            BeginFiring();
        }
    }

    private void TickFiring()
    {
        _stateTimer += Time.deltaTime;
        FaceTargetIfNeeded();

        Vector3 origin = ResolveOrigin();
        Vector3 target = ResolveTargetPoint();
        UpdateBeamVisual(origin, target);
        TickBeamDamage(origin, target);

        if (_stateTimer >= Mathf.Max(0.05f, _beamDuration))
        {
            BeginCooldown();
        }
    }

    private void TickCooldown()
    {
        _stateTimer += Time.deltaTime;
        HideBeamImmediate();
        UpdateGlyphVisual(Mathf.Lerp(0.58f, 0.32f, Mathf.Clamp01(_stateTimer / Mathf.Max(0.05f, _cooldownDuration))));

        if (_stateTimer >= Mathf.Max(0.05f, _cooldownDuration))
        {
            _state = BeamState.Dormant;
            _stateTimer = 0f;
            _activationTimer = 0.2f;
        }
    }

    private void BeginLocking()
    {
        if (_targetCandidate == null)
        {
            ResolveTargetIfNeeded(true);
        }

        if (_targetCandidate == null)
        {
            return;
        }

        _state = BeamState.Locking;
        _stateTimer = 0f;
        _damageTickTimer = 0f;
        HideBeamImmediate();
        EmitBurst(_originParticles, ResolveOrigin(), 18, 0.09f, 0.9f);
    }

    private void BeginFiring()
    {
        _state = BeamState.Firing;
        _stateTimer = 0f;
        _damageTickTimer = 0f;

        Vector3 origin = ResolveOrigin();
        Vector3 target = ResolveTargetPoint();
        EmitBurst(_originParticles, origin, 34, 0.12f, 1.4f);
        EmitBurst(_impactParticles, target, 28, 0.1f, 1.2f);
        SetParticleRate(_originParticles, 34f);
        SetParticleRate(_impactParticles, 42f);
        SetParticleRate(_driftParticles, 14f);
    }

    private void BeginCooldown()
    {
        Vector3 target = ResolveTargetPoint();
        EmitBurst(_impactParticles, target, 36, 0.12f, 1.6f);
        SetParticleRate(_originParticles, 0f);
        SetParticleRate(_impactParticles, 0f);
        SetParticleRate(_driftParticles, 0f);

        _state = BeamState.Cooldown;
        _stateTimer = 0f;
    }

    private void ResolveTargetIfNeeded(bool force = false)
    {
        if (_target != null)
        {
            _targetCandidate = _target;
            return;
        }

        if (!force && _targetCandidate != null)
        {
            return;
        }

        _targetRefreshTimer -= Time.deltaTime;
        if (!force && _targetRefreshTimer > 0f)
        {
            return;
        }

        _targetRefreshTimer = TargetRefreshInterval;
        _targetCandidate = null;

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

        GameObject taggedTarget = null;
        try
        {
            taggedTarget = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            taggedTarget = null;
        }

        if (taggedTarget != null)
        {
            _targetCandidate = taggedTarget.transform;
        }
    }

    private void FaceTargetIfNeeded()
    {
        if (!_faceTargetWhileActive || _targetCandidate == null)
        {
            return;
        }

        Vector3 toTarget = _targetCandidate.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * FacingTurnSpeed);
    }

    private Vector3 ResolveOrigin()
    {
        if (_beamOrigin != null)
        {
            return _beamOrigin.position;
        }

        return transform.TransformPoint(_originLocalOffset);
    }

    private Vector3 ResolveTargetPoint()
    {
        if (_targetCandidate == null)
        {
            return ResolveOrigin() + transform.forward * 4f;
        }

        return _targetCandidate.position + _targetOffset;
    }

    private void UpdateLockLine(float normalized)
    {
        Vector3 origin = ResolveOrigin();
        Vector3 target = ResolveTargetPoint();
        Vector3 midpoint = Vector3.Lerp(origin, target, 0.5f) + Vector3.up * (0.25f + normalized * 0.18f);
        Vector3[] path =
        {
            origin,
            midpoint,
            target
        };

        float pulse = 0.52f + Mathf.Sin(Time.time * 16f) * 0.18f;
        ApplyLine(_lockLine, path, _runeColor, _coreBeamColor, pulse, Mathf.Lerp(0.035f, 0.11f, normalized));
        UpdateTargetRing(target, origin, 0.28f + normalized * 0.28f, 0.45f + pulse * 0.25f);
    }

    private void UpdateBeamVisual(Vector3 origin, Vector3 target)
    {
        UpdateGlyphVisual(0.95f);

        float pulse = 0.82f + Mathf.Sin(Time.time * 20f + _noiseSeed) * 0.12f;
        Vector3[] outerPath = BuildBeamPath(origin, target, 0.08f, Time.time * 12f);
        Vector3[] corePath = BuildBeamPath(origin, target, 0.018f, Time.time * 16f + 1.7f);
        Vector3[] wispPathA = BuildBeamPath(origin, target, 0.2f, Time.time * 7.5f + 0.6f);
        Vector3[] wispPathB = BuildBeamPath(origin, target, -0.18f, Time.time * 8.25f + 2.4f);

        ApplyLine(_outerBeamLine, outerPath, _outerBeamColor, _edgeBeamColor, pulse, 0.5f);
        ApplyLine(_edgeBeamLine, outerPath, _edgeBeamColor, _outerBeamColor, 0.9f, 0.24f);
        ApplyLine(_coreBeamLine, corePath, _coreBeamColor, _coreBeamColor, 1f, 0.11f);
        ApplyLine(_wispLineA, wispPathA, _runeColor, _outerBeamColor, 0.28f, 0.08f);
        ApplyLine(_wispLineB, wispPathB, _outerBeamColor, _runeColor, 0.22f, 0.065f);

        UpdateTargetRing(target, origin, 0.44f + Mathf.Sin(Time.time * 9f) * 0.04f, 0.95f);
        UpdateParticlePositions(origin, target);
    }

    private Vector3[] BuildBeamPath(Vector3 origin, Vector3 target, float amplitude, float phase)
    {
        Vector3 delta = target - origin;
        float distance = delta.magnitude;
        Vector3 forward = distance > 0.0001f ? delta / distance : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = transform.right;
        }

        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right).normalized;

        Vector3[] path = new Vector3[_beamPointCount];
        for (int i = 0; i < path.Length; i++)
        {
            float u = i / (float)(path.Length - 1);
            float envelope = Mathf.Sin(u * Mathf.PI);
            float sideWave = Mathf.Sin(u * Mathf.PI * 4.4f + phase + _noiseSeed) * amplitude;
            float verticalWave = Mathf.Cos(u * Mathf.PI * 3.1f + phase * 0.72f) * amplitude * 0.45f;
            path[i] = Vector3.Lerp(origin, target, u) + (right * sideWave + up * verticalWave) * envelope;
        }

        return path;
    }

    private void UpdateGlyphVisual(float alpha)
    {
        Vector3 origin = ResolveOrigin();
        Vector3 target = ResolveTargetPoint();
        Vector3 direction = target - origin;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();
        float phase = Time.time * 1.6f + _noiseSeed;
        Color runeStart = _runeColor;
        Color runeEnd = _coreBeamColor;

        UpdateCircle(_baseRingLine, transform.position + Vector3.up * 0.18f, Vector3.up, 0.72f, runeStart, runeStart, alpha * 0.45f, phase);
        UpdateCircle(_topRingLine, origin + Vector3.up * 0.42f, direction, 0.34f, runeStart, runeEnd, alpha * 0.65f, -phase * 1.35f);
        UpdateCircle(_originRingLine, origin, direction, 0.27f + Mathf.Sin(Time.time * 6f) * 0.025f, runeEnd, runeStart, alpha, phase * 1.8f);

        Vector3 right = Vector3.Cross(Vector3.up, direction);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = transform.right;
        }

        right.Normalize();
        Vector3 up = Vector3.Cross(direction, right).normalized;
        Vector3 crossbarCenter = origin + up * 0.45f;
        ApplyLine(_crossbarLine, new[] { crossbarCenter - right * 0.46f, crossbarCenter + right * 0.46f }, runeStart, runeEnd, alpha * 0.7f, 0.045f);
        UpdateRuneTriangle(origin + direction * 0.018f, direction, right, up, 0.31f, alpha * 0.68f);
    }

    private void UpdateTargetRing(Vector3 target, Vector3 origin, float radius, float alpha)
    {
        Vector3 direction = target - origin;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();
        UpdateCircle(_impactRingLine, target, direction, radius, _coreBeamColor, _edgeBeamColor, alpha, -Time.time * 3.5f);
    }

    private void UpdateCircle(LineRenderer lineRenderer, Vector3 center, Vector3 normal, float radius, Color startColor, Color endColor, float alpha, float phase)
    {
        if (lineRenderer == null)
        {
            return;
        }

        if (normal.sqrMagnitude <= 0.0001f)
        {
            normal = Vector3.up;
        }

        normal.Normalize();
        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude <= 0.0001f)
        {
            tangent = Vector3.Cross(normal, Vector3.right);
        }

        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
        Vector3[] points = new Vector3[CircleSegments + 1];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = phase + i / (float)CircleSegments * Mathf.PI * 2f;
            points[i] = center + (Mathf.Cos(angle) * tangent + Mathf.Sin(angle) * bitangent) * radius;
        }

        ApplyLine(lineRenderer, points, startColor, endColor, alpha, lineRenderer.widthMultiplier);
    }

    private void UpdateRuneTriangle(Vector3 center, Vector3 normal, Vector3 right, Vector3 up, float radius, float alpha)
    {
        Vector3[] points = new Vector3[4];
        for (int i = 0; i < 3; i++)
        {
            float angle = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f + Time.time * 0.65f;
            points[i] = center + (Mathf.Cos(angle) * right + Mathf.Sin(angle) * up) * radius;
        }

        points[3] = points[0];
        ApplyLine(_runeTriangleLine, points, _runeColor, _coreBeamColor, alpha, 0.035f);
    }

    private void ApplyLine(LineRenderer lineRenderer, Vector3[] points, Color startColor, Color endColor, float alpha, float width)
    {
        if (lineRenderer == null)
        {
            return;
        }

        startColor.a *= alpha;
        endColor.a *= alpha;
        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        lineRenderer.widthMultiplier = Mathf.Max(0.001f, width);
        lineRenderer.enabled = alpha > 0.02f;
    }

    private void HideBeamImmediate()
    {
        SetLineEnabled(_lockLine, false);
        SetLineEnabled(_outerBeamLine, false);
        SetLineEnabled(_edgeBeamLine, false);
        SetLineEnabled(_coreBeamLine, false);
        SetLineEnabled(_wispLineA, false);
        SetLineEnabled(_wispLineB, false);
        SetLineEnabled(_impactRingLine, false);
        SetParticleRate(_originParticles, 0f);
        SetParticleRate(_impactParticles, 0f);
        SetParticleRate(_driftParticles, 0f);
    }

    private static void SetLineEnabled(LineRenderer lineRenderer, bool enabled)
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = enabled;
        }
    }

    private void TickBeamDamage(Vector3 origin, Vector3 target)
    {
        float tickInterval = Mathf.Max(0.02f, _damageTickInterval);
        _damageTickTimer += Time.deltaTime;
        while (_damageTickTimer >= tickInterval)
        {
            _damageTickTimer -= tickInterval;
            ApplyBeamDamage(origin, target, _beamDamagePerSecond * tickInterval);
        }
    }

    private void ApplyBeamDamage(Vector3 origin, Vector3 target, float damage)
    {
        if (damage <= 0f)
        {
            return;
        }

        _damageReceiversThisTick.Clear();

        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance > 0.0001f)
        {
            direction /= distance;
            int hitCount = Physics.SphereCastNonAlloc(origin, Mathf.Max(0.01f, _beamRadius), direction, s_damageHits, distance, _damageMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = s_damageHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                {
                    continue;
                }

                Vector3 hitPoint = hit.point.sqrMagnitude > 0.0001f ? hit.point : hitCollider.ClosestPoint(target);
                TryApplyDamage(hitCollider, damage, hitPoint, direction);
                s_damageHits[i] = default;
            }
        }

        if (_targetCandidate != null)
        {
            TryApplyDamage(_targetCandidate, damage, target, direction.sqrMagnitude > 0.0001f ? direction : transform.forward);
        }
    }

    private void TryApplyDamage(Component component, float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (component == null)
        {
            return;
        }

        if (!CombatDamageUtility.TryGetDamageReceiver(component, out ICombatDamageReceiver receiver))
        {
            return;
        }

        ApplyDamageToReceiver(receiver, damage, hitPoint, hitDirection);
    }

    private void TryApplyDamage(Transform target, float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (target == null)
        {
            return;
        }

        if (CombatDamageUtility.TryGetDamageReceiver(target, out ICombatDamageReceiver receiver))
            ApplyDamageToReceiver(receiver, damage, hitPoint, hitDirection);
    }

    private void ApplyDamageToReceiver(ICombatDamageReceiver receiver, float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (receiver == null || _damageReceiversThisTick.Contains(receiver))
        {
            return;
        }

        _damageReceiversThisTick.Add(receiver);
        CombatDamageUtility.ApplyDamageTo(receiver, damage, hitPoint, hitDirection, gameObject);
    }

    private void UpdateParticlePositions(Vector3 origin, Vector3 target)
    {
        if (_originParticles != null)
        {
            _originParticles.transform.position = origin;
        }

        if (_impactRoot != null)
        {
            _impactRoot.position = target;
        }

        if (_impactParticles != null)
        {
            _impactParticles.transform.position = target;
        }

        if (_driftParticles != null)
        {
            _driftParticles.transform.position = Vector3.Lerp(origin, target, 0.5f);
        }
    }

    private void EmitBurst(ParticleSystem particleSystem, Vector3 position, int count, float size, float speed)
    {
        if (particleSystem == null || count <= 0)
        {
            return;
        }

        particleSystem.transform.position = position;
        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            startColor = Color.Lerp(_edgeBeamColor, _coreBeamColor, 0.55f),
            startSize = size,
            velocity = Random.insideUnitSphere * speed
        };

        particleSystem.Emit(emitParams, count);
    }

    private static void SetParticleRate(ParticleSystem particleSystem, float rate)
    {
        if (particleSystem == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = rate;

        if (rate > 0f && !particleSystem.isPlaying)
        {
            particleSystem.Play();
        }
        else if (rate <= 0f && particleSystem.isPlaying)
        {
            particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void EnsureRuntimeMaterials()
    {
        if (_beamMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        _beamMaterial = CreateMaterial("Runtime Robot Anchor Beam", shader, null);
        _runeMaterial = CreateMaterial("Runtime Robot Anchor Runes", shader, null);
        _particleMaterial = CreateMaterial("Runtime Robot Anchor Soft Particles", shader, GetSoftCircleTexture());
    }

    private static Material CreateMaterial(string name, Shader shader, Texture texture)
    {
        Material material = new Material(shader)
        {
            name = name,
            renderQueue = (int)RenderQueue.Transparent
        };

        if (texture != null)
        {
            material.mainTexture = texture;
        }

        return material;
    }

    private void EnsureVisuals()
    {
        if (_rigRoot != null)
        {
            return;
        }

        EnsureRuntimeMaterials();

        _rigRoot = new GameObject("RobotAnchorBeamVfxRuntime");
        _rigRoot.transform.SetParent(transform, false);
        _rigRoot.layer = gameObject.layer;

        _baseRingLine = CreateLine("AnchorBaseRing", _runeMaterial, 0.035f, 5);
        _topRingLine = CreateLine("AnchorTopRing", _runeMaterial, 0.045f, 6);
        _originRingLine = CreateLine("BeamOriginRing", _runeMaterial, 0.055f, 7);
        _impactRingLine = CreateLine("BeamImpactRing", _runeMaterial, 0.055f, 9);
        _crossbarLine = CreateLine("AnchorCrossbar", _runeMaterial, 0.045f, 6);
        _runeTriangleLine = CreateLine("AnchorRuneTriangle", _runeMaterial, 0.035f, 7);
        _lockLine = CreateLine("TrackingLockLine", _beamMaterial, 0.055f, 8);
        _outerBeamLine = CreateLine("BeamOuterBlue", _beamMaterial, 0.5f, 10);
        _edgeBeamLine = CreateLine("BeamCyanEdge", _beamMaterial, 0.22f, 11);
        _coreBeamLine = CreateLine("BeamWhiteCore", _beamMaterial, 0.11f, 12);
        _wispLineA = CreateLine("BeamWispA", _beamMaterial, 0.08f, 9);
        _wispLineB = CreateLine("BeamWispB", _beamMaterial, 0.065f, 9);

        _impactRoot = new GameObject("BeamImpactFollower").transform;
        _impactRoot.SetParent(_rigRoot.transform, true);
        _impactRoot.gameObject.layer = gameObject.layer;

        _originParticles = CreateParticleSystem("BeamOriginParticles", 0.08f, 0.42f, 0.1f);
        _impactParticles = CreateParticleSystem("BeamImpactParticles", 0.075f, 0.36f, 0.16f);
        _driftParticles = CreateParticleSystem("BeamDriftParticles", 0.055f, 0.5f, 0.32f);

        SetObjectLayerRecursive(_rigRoot, gameObject.layer);
    }

    private LineRenderer CreateLine(string objectName, Material material, float width, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(_rigRoot.transform, false);
        lineObject.layer = gameObject.layer;

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.numCapVertices = 6;
        lineRenderer.numCornerVertices = 6;
        lineRenderer.widthMultiplier = width;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.sharedMaterial = material;
        lineRenderer.sortingOrder = sortingOrder;
        return lineRenderer;
    }

    private ParticleSystem CreateParticleSystem(string objectName, float startSize, float lifetime, float shapeRadius)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(_rigRoot.transform, false);
        particleObject.layer = gameObject.layer;

        ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particleSystem.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 1.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.55f, startSize * 1.6f);
        main.startColor = new ParticleSystem.MinMaxGradient(_edgeBeamColor, _coreBeamColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 180;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = shapeRadius;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(_coreBeamColor, 0f),
                new GradientColorKey(_edgeBeamColor, 0.42f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.12f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 13;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return particleSystem;
    }

    private static Texture2D GetSoftCircleTexture()
    {
        if (s_softCircleTexture != null)
        {
            return s_softCircleTexture;
        }

        const int size = 64;
        s_softCircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Soft Energy Circle",
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
                s_softCircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        s_softCircleTexture.Apply();
        return s_softCircleTexture;
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

    private static void SetObjectLayerRecursive(GameObject targetObject, int layer)
    {
        if (targetObject == null)
        {
            return;
        }

        targetObject.layer = layer;
        for (int i = 0; i < targetObject.transform.childCount; i++)
        {
            SetObjectLayerRecursive(targetObject.transform.GetChild(i).gameObject, layer);
        }
    }
}
