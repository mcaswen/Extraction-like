using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class TracerAnchorVortexVfx : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform _target;
    [SerializeField] private string _targetObjectName = "Player";
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.85f, 0f);

    [Header("Boss References")]
    [SerializeField] private Transform _meleeOrigin;
    [SerializeField] private Transform _projectileOrigin;
    [SerializeField] private HunterBossBehaviorController _bossController;

    [Header("Auto Preview")]
    [SerializeField] private bool _autoPreviewWithoutBossController;
    [SerializeField] private float _previewInitialDelay = 0.6f;
    [SerializeField] private float _previewMeleeInterval = 2.4f;
    [SerializeField] private int _vortexTriggerAttackCount = 3;
    [SerializeField] private bool _allowKeyboardPreview = true;
    [SerializeField] private KeyCode _previewMeleeKey = KeyCode.Y;
    [SerializeField] private KeyCode _previewComboKey = KeyCode.O;

    [Header("Melee Sweep")]
    [SerializeField] private float _meleeRadius = 2.4f;
    [SerializeField] private float _meleeDuration = 0.42f;
    [SerializeField] private float _meleeAnchorScale = 0.34f;

    [Header("Vortex Combo")]
    [SerializeField] private float _vortexRadius = 3f;
    [SerializeField] private float _vortexChargeDuration = 1.25f;
    [SerializeField] private float _vortexImmobilizeDuration = 1.65f;
    [SerializeField] private float _anchorThrowDuration = 0.55f;
    [SerializeField] private float _fallbackAnchorDamage = 16f;
    [SerializeField] private float _fallbackAnchorKnockback = 5.5f;

    [Header("Visual")]
    [SerializeField] private int _arcPointCount = 28;
    [SerializeField] private int _vortexPointCount = 72;
    [SerializeField] private Color _anchorMetalColor = new Color(0.17f, 0.18f, 0.22f, 1f);
    [SerializeField] private Color _anchorEdgeColor = new Color(0.13f, 0.75f, 1f, 0.95f);
    [SerializeField] private Color _vortexOuterColor = new Color(0.02f, 0.08f, 0.16f, 0.78f);
    [SerializeField] private Color _vortexCoreColor = new Color(0.28f, 0.95f, 1f, 0.98f);
    [SerializeField] private Color _chainColor = new Color(0.42f, 0.28f, 0.18f, 0.95f);

    private static Texture2D s_softParticleTexture;

    private Material _anchorMaterial;
    private Material _energyMaterial;
    private Material _chainMaterial;
    private Material _particleMaterial;
    private Coroutine _meleeRoutine;
    private Coroutine _comboRoutine;
    private Coroutine _previewRoutine;
    private Coroutine _sceneLocalImmobilizeRoutine;
    private CharacterMotor _lockedCharacterMotor;
    private int _previewAttackCount;

    private void Awake()
    {
        _arcPointCount = Mathf.Max(8, _arcPointCount);
        _vortexPointCount = Mathf.Max(18, _vortexPointCount);
        EnsureReferences();
        EnsureMaterials();
    }

    private void OnEnable()
    {
        if (_autoPreviewWithoutBossController && _bossController == null)
        {
            _previewRoutine = StartCoroutine(AutoPreviewLoop());
        }
    }

    private void OnDisable()
    {
        StopRunningRoutine(ref _meleeRoutine);
        StopRunningRoutine(ref _comboRoutine);
        StopRunningRoutine(ref _previewRoutine);
        RestoreSceneLocalImmobilize();
        DestroyMaterial(ref _anchorMaterial);
        DestroyMaterial(ref _energyMaterial);
        DestroyMaterial(ref _chainMaterial);
        DestroyMaterial(ref _particleMaterial);
    }

    private void Update()
    {
        EnsureReferences();

        if (!_allowKeyboardPreview)
        {
            return;
        }

        if (Input.GetKeyDown(_previewMeleeKey))
        {
            PlayMeleeSweep();
        }

        if (Input.GetKeyDown(_previewComboKey))
        {
            PlayVortexAnchorCombo();
        }
    }

    public void PlayMeleeSweep()
    {
        if (_meleeRoutine != null)
        {
            StopCoroutine(_meleeRoutine);
        }

        _meleeRoutine = StartCoroutine(PlayMeleeSweepRoutine());
    }

    public void PlayVortexAnchorCombo()
    {
        Vector3 center = ResolveTargetGroundPosition();
        PlayVortexAnchorCombo(center);
    }

    public void PlayVortexAnchorCombo(Vector3 vortexCenter)
    {
        if (_comboRoutine != null)
        {
            StopCoroutine(_comboRoutine);
        }

        _comboRoutine = StartCoroutine(PlayVortexAnchorComboRoutine(vortexCenter));
    }

    public void PlayAnchorThrow(Vector3 targetPoint)
    {
        StartCoroutine(PlayAnchorThrowRoutine(targetPoint, false));
    }

    private IEnumerator AutoPreviewLoop()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, _previewInitialDelay));
        while (enabled)
        {
            PlayMeleeSweep();
            _previewAttackCount++;
            if (_previewAttackCount >= Mathf.Max(1, _vortexTriggerAttackCount))
            {
                _previewAttackCount = 0;
                PlayVortexAnchorCombo();
                yield return new WaitForSeconds(_vortexChargeDuration + _vortexImmobilizeDuration + _anchorThrowDuration + 0.35f);
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, _previewMeleeInterval));
        }
    }

    private IEnumerator PlayMeleeSweepRoutine()
    {
        EnsureMaterials();
        GameObject root = new GameObject("TracerAnchorMeleeSweepVfx");
        root.layer = gameObject.layer;

        LineRenderer trailLine = CreateLine(root.transform, "AnchorSweepTrail", _energyMaterial, 0.34f, 19);
        LineRenderer rimLine = CreateLine(root.transform, "AnchorSweepRim", _energyMaterial, 0.08f, 20);
        GameObject anchor = CreateAnchorVisual("MeleeAnchorVisual", _meleeAnchorScale);
        anchor.transform.SetParent(root.transform, true);

        ParticleSystem sparks = CreateParticles(root.transform, "AnchorSweepSparks", 0.08f, 0.36f, 2.2f, 0f, 21);
        Transform originTransform = _meleeOrigin != null ? _meleeOrigin : transform;
        float duration = Mathf.Max(0.05f, _meleeDuration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / duration);
            float angle = Mathf.Lerp(-115f, 115f, SmoothOut(normalized));
            Vector3 center = originTransform.position + Vector3.up * 0.12f;
            Vector3 forward = FlattenedForward();
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 anchorPosition = center + direction * Mathf.Max(0.2f, _meleeRadius);
            Vector3 tangent = Quaternion.AngleAxis(90f, Vector3.up) * direction;

            anchor.transform.position = anchorPosition;
            anchor.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up) * Quaternion.Euler(75f, 0f, 0f);
            anchor.transform.localScale = Vector3.one * _meleeAnchorScale * Mathf.Lerp(0.85f, 1.15f, Mathf.Sin(normalized * Mathf.PI));

            Vector3[] arc = BuildArc(center, _meleeRadius, -115f, angle, 0.14f + normalized * 0.22f);
            ApplyLine(trailLine, arc, _anchorEdgeColor, _vortexCoreColor, 1f - normalized * 0.35f, 0.34f);
            ApplyLine(rimLine, arc, Color.white, _anchorEdgeColor, 1f - normalized * 0.15f, 0.08f);

            if (normalized > 0.15f && normalized < 0.78f)
            {
                sparks.transform.position = anchorPosition;
                sparks.Emit(2);
            }

            yield return null;
        }

        Destroy(root, 0.25f);
        _meleeRoutine = null;
    }

    private IEnumerator PlayVortexAnchorComboRoutine(Vector3 vortexCenter)
    {
        EnsureMaterials();
        vortexCenter.y += 0.035f;
        GameObject root = new GameObject("TracerAnchorVortexComboVfx");
        root.layer = gameObject.layer;

        LineRenderer outerSwirl = CreateLine(root.transform, "VortexOuterSwirl", _energyMaterial, 0.42f, 13);
        LineRenderer coreSwirl = CreateLine(root.transform, "VortexCoreSwirl", _energyMaterial, 0.18f, 14);
        LineRenderer chainA = CreateLine(root.transform, "VortexChainA", _chainMaterial, 0.08f, 15);
        LineRenderer chainB = CreateLine(root.transform, "VortexChainB", _chainMaterial, 0.08f, 15);
        ParticleSystem foam = CreateParticles(root.transform, "VortexFoam", 0.12f, 0.55f, 1.2f, 50f, 16);
        foam.transform.position = vortexCenter + Vector3.up * 0.08f;

        float totalDuration = Mathf.Max(0.05f, _vortexChargeDuration + _vortexImmobilizeDuration);
        float timer = 0f;
        bool immobilizeApplied = false;

        while (timer < totalDuration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / totalDuration);
            float charge = Mathf.Clamp01(timer / Mathf.Max(0.05f, _vortexChargeDuration));
            float armed = Mathf.Clamp01((timer - _vortexChargeDuration) / Mathf.Max(0.05f, _vortexImmobilizeDuration));
            float visibleRadius = Mathf.Lerp(0.35f, _vortexRadius, Mathf.SmoothStep(0f, 1f, charge));
            float rotation = Time.time * 175f;

            ApplyLine(outerSwirl, BuildSpiral(vortexCenter, visibleRadius, 2.15f, rotation, 0.05f), _vortexOuterColor, _anchorEdgeColor, 0.95f - normalized * 0.28f, 0.42f);
            ApplyLine(coreSwirl, BuildSpiral(vortexCenter + Vector3.up * 0.02f, visibleRadius * 0.68f, 2.6f, -rotation * 1.25f, 0.08f), _vortexCoreColor, Color.white, 1f - normalized * 0.12f, 0.18f);

            Vector3 targetPoint = ResolveTargetPoint();
            DrawChain(chainA, vortexCenter, targetPoint, rotation, armed, 0f);
            DrawChain(chainB, vortexCenter, targetPoint, rotation, armed, 180f);

            if (!immobilizeApplied && timer >= _vortexChargeDuration)
            {
                immobilizeApplied = true;
                ApplyImmobilizeFallback(_vortexImmobilizeDuration);
                BurstAt(targetPoint, 28, 0.1f, 1.35f);
            }

            yield return null;
        }

        foam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (_bossController == null)
        {
            yield return PlayAnchorThrowRoutine(ResolveTargetPoint(), true);
        }

        Destroy(root, 0.5f);
        _comboRoutine = null;
    }

    private IEnumerator PlayAnchorThrowRoutine(Vector3 targetPoint, bool applyFallbackDamage)
    {
        EnsureMaterials();
        GameObject root = new GameObject("TracerThrownAnchorVfx");
        root.layer = gameObject.layer;

        LineRenderer chainLine = CreateLine(root.transform, "ThrownAnchorChain", _chainMaterial, 0.08f, 17);
        LineRenderer energyTrail = CreateLine(root.transform, "ThrownAnchorEnergyTrail", _energyMaterial, 0.24f, 18);
        GameObject anchor = CreateAnchorVisual("ThrownAnchorVisual", _meleeAnchorScale * 0.95f);
        anchor.transform.SetParent(root.transform, true);
        ParticleSystem sparks = CreateParticles(root.transform, "ThrownAnchorSparks", 0.07f, 0.34f, 3.4f, 0f, 19);

        Vector3 start = ResolveProjectileOrigin();
        Vector3 end = targetPoint;
        if (_target != null)
        {
            end = _target.position + _targetOffset;
        }

        float duration = Mathf.Max(0.05f, _anchorThrowDuration);
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / duration);
            Vector3 current = EvaluateArc(start, end, normalized, 1.15f);
            Vector3 previous = EvaluateArc(start, end, Mathf.Clamp01(normalized - 0.04f), 1.15f);
            Vector3 direction = current - previous;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = (end - start).normalized;
            }

            anchor.transform.position = current;
            anchor.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(85f, 0f, 0f);
            DrawThrownChain(chainLine, start, current, normalized);
            ApplyLine(energyTrail, BuildTrail(previous, current, 7), _vortexCoreColor, _anchorEdgeColor, 1f - normalized * 0.2f, 0.24f);
            sparks.transform.position = current;
            sparks.Emit(2);
            yield return null;
        }

        if (applyFallbackDamage)
        {
            ApplyAnchorFallbackDamage(end, (end - start).normalized);
        }

        BurstAt(end, 34, 0.12f, 1.65f);
        Destroy(root, 0.45f);
    }

    private void ApplyAnchorFallbackDamage(Vector3 hitPoint, Vector3 direction)
    {
        if (_bossController != null)
        {
            return;
        }

        Transform target = ResolveTarget();
        if (target == null)
        {
            return;
        }

        if (Vector3.Distance(target.position, hitPoint) > 1.15f)
        {
            return;
        }

        CombatDamageUtility.ApplyDamageTo(target, _fallbackAnchorDamage, hitPoint, direction, gameObject);

        PlayerMovementController playerMovement = target.GetComponent<PlayerMovementController>();
        if (playerMovement == null)
        {
            playerMovement = target.GetComponentInChildren<PlayerMovementController>();
        }

        if (playerMovement != null)
        {
            playerMovement.ApplyExternalImpulse(direction, _fallbackAnchorKnockback);
            return;
        }

        CharacterController controller = target.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = target.GetComponentInChildren<CharacterController>();
        }

        if (controller != null)
        {
            StartCoroutine(ApplyControllerKnockback(controller, direction, _fallbackAnchorKnockback * 0.14f, 0.18f));
        }
    }

    private IEnumerator ApplyControllerKnockback(CharacterController controller, Vector3 direction, float distance, float duration)
    {
        float timer = 0f;
        Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (horizontal.sqrMagnitude <= 0.0001f)
        {
            horizontal = FlattenedForward();
        }

        horizontal.Normalize();
        while (controller != null && timer < duration)
        {
            float delta = Time.deltaTime;
            timer += delta;
            controller.Move(horizontal * (distance / Mathf.Max(0.01f, duration)) * delta);
            yield return null;
        }
    }

    private void ApplyImmobilizeFallback(float duration)
    {
        Transform target = ResolveTarget();
        if (target == null)
        {
            return;
        }

        PlayerMovementController playerMovement = target.GetComponent<PlayerMovementController>();
        if (playerMovement == null)
        {
            playerMovement = target.GetComponentInChildren<PlayerMovementController>();
        }

        if (playerMovement != null)
        {
            playerMovement.ApplyImmobilize(duration);
            return;
        }

        CharacterMotor motor = target.GetComponent<CharacterMotor>();
        if (motor == null)
        {
            motor = target.GetComponentInChildren<CharacterMotor>();
        }

        if (motor == null)
        {
            return;
        }

        RestoreSceneLocalImmobilize();
        _lockedCharacterMotor = motor;
        _lockedCharacterMotor.SetMoveLocked(true);
        _sceneLocalImmobilizeRoutine = StartCoroutine(SceneLocalImmobilizeCountdown(duration));
    }

    private IEnumerator SceneLocalImmobilizeCountdown(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        RestoreSceneLocalImmobilize();
    }

    private void RestoreSceneLocalImmobilize()
    {
        if (_sceneLocalImmobilizeRoutine != null)
        {
            StopCoroutine(_sceneLocalImmobilizeRoutine);
            _sceneLocalImmobilizeRoutine = null;
        }

        if (_lockedCharacterMotor != null)
        {
            _lockedCharacterMotor.SetMoveLocked(false);
            _lockedCharacterMotor = null;
        }
    }

    private void EnsureReferences()
    {
        if (_bossController == null)
        {
            _bossController = GetComponentInParent<HunterBossBehaviorController>();
        }

        if (_bossController != null)
        {
            if (_target == null && _bossController.PlayerTransform != null)
            {
                _target = _bossController.PlayerTransform;
            }

            if (_meleeOrigin == null)
            {
                _meleeOrigin = _bossController.MeleeOrigin;
            }

            if (_projectileOrigin == null)
            {
                _projectileOrigin = _bossController.ProjectileOrigin;
            }

            _meleeRadius = _bossController.MeleeAttackRadius > 0f ? _bossController.MeleeAttackRadius : _meleeRadius;
            _vortexRadius = _bossController.VortexRadius > 0f ? _bossController.VortexRadius : _vortexRadius;
            _vortexChargeDuration = _bossController.VortexChargeDuration > 0f ? _bossController.VortexChargeDuration : _vortexChargeDuration;
            _vortexImmobilizeDuration = _bossController.VortexImmobilizeDuration > 0f ? _bossController.VortexImmobilizeDuration : _vortexImmobilizeDuration;
            _fallbackAnchorDamage = _bossController.AnchorThrowDamage > 0f ? _bossController.AnchorThrowDamage : _fallbackAnchorDamage;
            _fallbackAnchorKnockback = _bossController.AnchorThrowKnockback > 0f ? _bossController.AnchorThrowKnockback : _fallbackAnchorKnockback;
        }

        ResolveTarget();
    }

    private Transform ResolveTarget()
    {
        if (_target != null)
        {
            return _target;
        }

        if (!string.IsNullOrWhiteSpace(_targetObjectName))
        {
            GameObject found = GameObject.Find(_targetObjectName);
            if (found != null)
            {
                _target = found.transform;
                return _target;
            }
        }

        if (PlayerTargetResolver.TryGetCurrentPlayerTransform(transform, out Transform currentPlayer))
        {
            _target = currentPlayer;
        }

        return _target;
    }

    private Vector3 ResolveTargetPoint()
    {
        Transform target = ResolveTarget();
        return target != null ? target.position + _targetOffset : transform.position + FlattenedForward() * _meleeRadius + _targetOffset;
    }

    private Vector3 ResolveTargetGroundPosition()
    {
        Transform target = ResolveTarget();
        Vector3 position = target != null ? target.position : transform.position + FlattenedForward() * 2f;
        position.y = 0.035f;
        return position;
    }

    private Vector3 ResolveProjectileOrigin()
    {
        if (_projectileOrigin != null)
        {
            return _projectileOrigin.position;
        }

        return transform.position + Vector3.up * 1.25f + FlattenedForward() * 0.65f;
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

    private Vector3[] BuildArc(Vector3 center, float radius, float startAngle, float endAngle, float height)
    {
        Vector3[] points = new Vector3[_arcPointCount];
        Vector3 forward = FlattenedForward();
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            points[i] = center + direction * radius + Vector3.up * height;
        }

        return points;
    }

    private Vector3[] BuildSpiral(Vector3 center, float radius, float turns, float rotationDegrees, float height)
    {
        Vector3[] points = new Vector3[_vortexPointCount];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            float currentRadius = Mathf.Lerp(radius, 0.12f, t);
            float angle = rotationDegrees + t * turns * 360f;
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * currentRadius;
            points[i] = center + offset + Vector3.up * (height + Mathf.Sin(t * Mathf.PI * 4f + Time.time * 6f) * 0.025f);
        }

        return points;
    }

    private void DrawChain(LineRenderer line, Vector3 center, Vector3 targetPoint, float rotation, float armed, float angleOffset)
    {
        if (line == null)
        {
            return;
        }

        float radius = Mathf.Lerp(_vortexRadius * 1.05f, 0.55f, Mathf.SmoothStep(0f, 1f, armed));
        Vector3 startOffset = new Vector3(Mathf.Cos((rotation + angleOffset) * Mathf.Deg2Rad), 0f, Mathf.Sin((rotation + angleOffset) * Mathf.Deg2Rad)) * radius;
        Vector3 start = center + startOffset + Vector3.up * 0.16f;
        Vector3 end = Vector3.Lerp(center + Vector3.up * 0.18f, targetPoint, Mathf.SmoothStep(0f, 1f, armed));
        Vector3[] points = BuildSagLine(start, end, 10, -0.06f);
        ApplyLine(line, points, _chainColor, _anchorEdgeColor, Mathf.Lerp(0.35f, 1f, armed), 0.08f);
    }

    private void DrawThrownChain(LineRenderer line, Vector3 start, Vector3 end, float normalized)
    {
        Vector3[] points = BuildSagLine(start, end, 12, Mathf.Lerp(0.1f, -0.18f, normalized));
        ApplyLine(line, points, _chainColor, _anchorEdgeColor, 0.95f, 0.08f);
    }

    private Vector3[] BuildSagLine(Vector3 start, Vector3 end, int count, float sag)
    {
        Vector3[] points = new Vector3[Mathf.Max(2, count)];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            points[i] = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * sag;
        }

        return points;
    }

    private Vector3[] BuildTrail(Vector3 start, Vector3 end, int count)
    {
        Vector3[] points = new Vector3[Mathf.Max(2, count)];
        Vector3 back = (start - end);
        if (back.sqrMagnitude <= 0.0001f)
        {
            back = -FlattenedForward();
        }

        back.Normalize();
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (float)(points.Length - 1);
            points[i] = Vector3.Lerp(end + back * 0.75f, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.08f;
        }

        return points;
    }

    private static Vector3 EvaluateArc(Vector3 start, Vector3 end, float normalized, float height)
    {
        Vector3 linear = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, normalized));
        linear.y += Mathf.Sin(normalized * Mathf.PI) * height;
        return linear;
    }

    private void ApplyLine(LineRenderer line, Vector3[] points, Color startColor, Color endColor, float alpha, float width)
    {
        if (line == null || points == null || points.Length < 2)
        {
            return;
        }

        line.enabled = alpha > 0.01f;
        line.positionCount = points.Length;
        line.SetPositions(points);
        startColor.a *= alpha;
        endColor.a *= alpha;
        line.startColor = startColor;
        line.endColor = endColor;
        line.widthMultiplier = width * Mathf.Clamp01(alpha);
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

    private GameObject CreateAnchorVisual(string objectName, float scale)
    {
        GameObject root = new GameObject(objectName);
        root.layer = gameObject.layer;

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "AnchorShaft";
        shaft.layer = gameObject.layer;
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localScale = new Vector3(0.09f, 0.78f, 0.09f);
        shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        ApplyRenderer(shaft, _anchorMaterial);

        GameObject cross = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cross.name = "AnchorCrossbar";
        cross.layer = gameObject.layer;
        cross.transform.SetParent(root.transform, false);
        cross.transform.localPosition = new Vector3(0f, 0f, 0.45f);
        cross.transform.localScale = new Vector3(0.64f, 0.08f, 0.08f);
        ApplyRenderer(cross, _anchorMaterial);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "AnchorHead";
        head.layer = gameObject.layer;
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 0f, -0.5f);
        head.transform.localScale = new Vector3(0.72f, 0.14f, 0.3f);
        ApplyRenderer(head, _anchorMaterial);

        GameObject leftFluke = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftFluke.name = "LeftFluke";
        leftFluke.layer = gameObject.layer;
        leftFluke.transform.SetParent(root.transform, false);
        leftFluke.transform.localPosition = new Vector3(-0.38f, 0f, -0.62f);
        leftFluke.transform.localRotation = Quaternion.Euler(0f, 0f, 34f);
        leftFluke.transform.localScale = new Vector3(0.1f, 0.1f, 0.42f);
        ApplyRenderer(leftFluke, _anchorMaterial);

        GameObject rightFluke = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightFluke.name = "RightFluke";
        rightFluke.layer = gameObject.layer;
        rightFluke.transform.SetParent(root.transform, false);
        rightFluke.transform.localPosition = new Vector3(0.38f, 0f, -0.62f);
        rightFluke.transform.localRotation = Quaternion.Euler(0f, 0f, -34f);
        rightFluke.transform.localScale = new Vector3(0.1f, 0.1f, 0.42f);
        ApplyRenderer(rightFluke, _anchorMaterial);

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ring.name = "AnchorRingGlow";
        ring.layer = gameObject.layer;
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0f, 0.78f);
        ring.transform.localScale = new Vector3(0.28f, 0.28f, 0.04f);
        ApplyRenderer(ring, _energyMaterial);

        root.transform.localScale = Vector3.one * scale;
        return root;
    }

    private void ApplyRenderer(GameObject target, Material material)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private ParticleSystem CreateParticles(Transform parent, string objectName, float size, float lifetime, float speed, float rate, int sortingOrder)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.layer = gameObject.layer;
        particleObject.transform.SetParent(parent, false);

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = rate > 0f;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.65f, size * 1.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(_vortexCoreColor, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 180;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.05f, _vortexRadius * 0.45f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(_vortexCoreColor, 0f),
                new GradientColorKey(_anchorEdgeColor, 0.45f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.16f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = _particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (rate > 0f)
        {
            particles.Play();
        }

        return particles;
    }

    private void BurstAt(Vector3 position, int count, float size, float speed)
    {
        GameObject burst = new GameObject("TracerAnchorVortexBurst");
        burst.layer = gameObject.layer;
        burst.transform.position = position;
        ParticleSystem particles = CreateParticles(burst.transform, "BurstParticles", size, 0.45f, speed, 0f, 23);
        particles.Emit(count);
        Destroy(burst, 1f);
    }

    private void EnsureMaterials()
    {
        if (_anchorMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        _anchorMaterial = CreateMaterial("Runtime Tracer Anchor Metal", shader, _anchorMetalColor, null);
        _energyMaterial = CreateMaterial("Runtime Tracer Anchor Energy", shader, _anchorEdgeColor, null);
        _chainMaterial = CreateMaterial("Runtime Tracer Chain", shader, _chainColor, null);
        _particleMaterial = CreateMaterial("Runtime Tracer Soft Vortex Particle", shader, _vortexCoreColor, GetSoftParticleTexture());
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
            name = "Runtime Tracer Vortex Soft Circle",
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

    private void StopRunningRoutine(ref Coroutine routine)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = null;
    }

    private static void DestroyMaterial(ref Material material)
    {
        if (material == null)
        {
            return;
        }

        Destroy(material);
        material = null;
    }

    private static float SmoothOut(float value)
    {
        value = Mathf.Clamp01(value);
        return 1f - Mathf.Pow(1f - value, 3f);
    }
}
