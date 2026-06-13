using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class SkeFishboneAttackVfx : MonoBehaviour
{
    public enum AttackSelection
    {
        Auto,
        MeleeOnly,
        RangedOnly
    }

    [Header("Target")]
    [SerializeField] private Transform _target;
    [SerializeField] private string _targetObjectName = "Player";
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 0.9f, 0f);

    [Header("Origin")]
    [SerializeField] private Transform _fishboneOrigin;
    [SerializeField] private Vector3 _originLocalOffset = new Vector3(0.18f, 1.15f, 0.36f);
    [SerializeField] private bool _faceTargetBeforeAttack = true;

    [Header("Attack Timing")]
    [SerializeField] private AttackSelection _attackSelection = AttackSelection.Auto;
    [SerializeField] private float _initialDelay = 0.65f;
    [SerializeField] private float _attackInterval = 2.35f;
    [SerializeField] private float _meleeDuration = 0.56f;
    [SerializeField] private float _rangedDuration = 0.9f;
    [SerializeField] private bool _allowKeyboardPreview = true;
    [SerializeField] private KeyCode _previewMeleeKey = KeyCode.J;
    [SerializeField] private KeyCode _previewRangedKey = KeyCode.K;

    [Header("Damage")]
    [SerializeField] private LayerMask _damageMask = ~0;
    [SerializeField] private float _meleeAttackRange = 2.65f;
    [SerializeField] private float _meleeDamageRadius = 1.65f;
    [SerializeField] private float _meleeDamage = 12f;
    [SerializeField] private float _minimumRangedDistance = 2.7f;
    [SerializeField] private float _rangedAttackRange = 7.8f;
    [SerializeField] private float _biteRadius = 0.82f;
    [SerializeField] private float _biteDamage = 15f;

    [Header("Visual")]
    [SerializeField] private int _boneSegmentCount = 14;
    [SerializeField] private int _pathPointCount = 24;
    [SerializeField] private Color _boneColor = new Color(0.92f, 0.9f, 0.78f, 1f);
    [SerializeField] private Color _boneShadowColor = new Color(0.04f, 0.04f, 0.035f, 0.72f);
    [SerializeField] private Color _energyColor = new Color(0.2f, 0.92f, 1f, 0.92f);
    [SerializeField] private Color _energyFadeColor = new Color(0.72f, 1f, 1f, 0.15f);

    private const int MinimumPathPoints = 8;
    private const int MinimumBoneSegments = 6;

    private Transform _targetCandidate;
    private Coroutine _activeAttack;
    private float _nextAttackTimer;
    private float _targetRefreshTimer;

    private Material _boneMaterial;
    private Material _shadowMaterial;
    private Material _energyMaterial;
    private Material _socketMaterial;
    private Material _particleMaterial;

    private Mesh _coneMesh;
    private static Texture2D s_softParticleTexture;

    private void Awake()
    {
        _pathPointCount = Mathf.Max(MinimumPathPoints, _pathPointCount);
        _boneSegmentCount = Mathf.Max(MinimumBoneSegments, _boneSegmentCount);
        _nextAttackTimer = Mathf.Max(0f, _initialDelay);
        EnsureRuntimeMaterials();
    }

    private void OnDisable()
    {
        if (_activeAttack != null)
        {
            StopCoroutine(_activeAttack);
            _activeAttack = null;
        }

        DestroyRuntimeMaterial(ref _boneMaterial);
        DestroyRuntimeMaterial(ref _shadowMaterial);
        DestroyRuntimeMaterial(ref _energyMaterial);
        DestroyRuntimeMaterial(ref _socketMaterial);
        DestroyRuntimeMaterial(ref _particleMaterial);

        if (_coneMesh != null)
        {
            Destroy(_coneMesh);
            _coneMesh = null;
        }
    }

    private void Update()
    {
        ResolveTargetIfNeeded();

        if (_allowKeyboardPreview && _activeAttack == null)
        {
            if (Input.GetKeyDown(_previewMeleeKey))
            {
                BeginAttack(true);
                return;
            }

            if (Input.GetKeyDown(_previewRangedKey))
            {
                BeginAttack(false);
                return;
            }
        }

        if (_activeAttack != null || _targetCandidate == null)
        {
            return;
        }

        _nextAttackTimer -= Time.deltaTime;
        if (_nextAttackTimer > 0f)
        {
            return;
        }

        float distance = PlanarDistance(transform.position, _targetCandidate.position);
        if (_attackSelection == AttackSelection.MeleeOnly ||
            (_attackSelection == AttackSelection.Auto && distance <= _meleeAttackRange))
        {
            BeginAttack(true);
            return;
        }

        if (_attackSelection == AttackSelection.RangedOnly ||
            (_attackSelection == AttackSelection.Auto &&
             distance >= _minimumRangedDistance &&
             distance <= _rangedAttackRange))
        {
            BeginAttack(false);
            return;
        }

        _nextAttackTimer = 0.2f;
    }

    private void BeginAttack(bool isMelee)
    {
        if (_targetCandidate == null)
        {
            ResolveTargetIfNeeded(true);
        }

        if (_targetCandidate == null)
        {
            return;
        }

        if (_faceTargetBeforeAttack)
        {
            FaceTarget();
        }

        _nextAttackTimer = Mathf.Max(0.15f, _attackInterval);
        _activeAttack = StartCoroutine(isMelee ? PlayMeleeAttack() : PlayRangedBiteAttack());
    }

    private IEnumerator PlayMeleeAttack()
    {
        FishboneVfxInstance fishbone = new FishboneVfxInstance(this, "SkeFishboneMelee", true);
        bool damageApplied = false;
        float duration = Mathf.Max(0.05f, _meleeDuration);

        SpawnEnergyBurst(ResolveOrigin(), transform.forward, 18, 0.08f, 1.7f, 0.45f);

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / duration);
            float sweep = Smooth01(normalized);
            float alpha = 1f - Mathf.SmoothStep(0.78f, 1f, normalized);

            Vector3 origin = ResolveOrigin();
            Vector3 forward = ResolveAttackForward();
            Vector3[] path = BuildMeleePath(origin, forward, sweep);
            fishbone.UpdatePath(path, alpha, 0.42f + 0.22f * Mathf.Sin(normalized * Mathf.PI), normalized);

            if (!damageApplied && normalized >= 0.42f)
            {
                damageApplied = true;
                Vector3 impactPoint = path[path.Length - 1];
                ApplyMeleeDamage(origin, forward);
                SpawnEnergyBurst(impactPoint, forward, 34, 0.1f, 2.8f, 0.65f);
            }

            yield return null;
        }

        fishbone.Destroy();
        _activeAttack = null;
    }

    private IEnumerator PlayRangedBiteAttack()
    {
        FishboneVfxInstance fishbone = new FishboneVfxInstance(this, "SkeFishboneBite", true);
        bool damageApplied = false;
        float duration = Mathf.Max(0.05f, _rangedDuration);

        SpawnEnergyBurst(ResolveOrigin(), transform.forward, 22, 0.07f, 1.5f, 0.42f);

        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / duration);
            float alpha = 1f - Mathf.SmoothStep(0.9f, 1f, normalized);
            float extension;

            if (normalized < 0.62f)
            {
                extension = Smooth01(normalized / 0.62f);
            }
            else if (normalized < 0.82f)
            {
                extension = 1f;
            }
            else
            {
                extension = 1f - Smooth01((normalized - 0.82f) / 0.18f) * 0.88f;
            }

            Vector3 origin = ResolveOrigin();
            Vector3 target = ResolveTargetPoint();
            Vector3 endpoint = Vector3.Lerp(origin, target, extension);
            Vector3[] path = BuildRangedPath(origin, endpoint, normalized);
            float jawOpen = BuildJawOpenAmount(normalized);
            fishbone.UpdatePath(path, alpha, jawOpen, normalized);

            if (!damageApplied && normalized >= 0.66f)
            {
                damageApplied = true;
                ApplyBiteDamage(origin, endpoint);
                SpawnEnergyBurst(endpoint, endpoint - origin, 42, 0.105f, 3.2f, 0.7f);
            }

            yield return null;
        }

        fishbone.Destroy();
        _activeAttack = null;
    }

    private void ResolveTargetIfNeeded(bool force = false)
    {
        if (!force && _targetCandidate != null)
        {
            return;
        }

        _targetRefreshTimer -= Time.deltaTime;
        if (!force && _targetRefreshTimer > 0f)
        {
            return;
        }

        _targetRefreshTimer = 0.4f;

        if (_target != null)
        {
            _targetCandidate = _target;
            return;
        }

        if (PlayerTargetResolver.TryGetCurrentPlayerTransform(transform, out Transform currentPlayer))
        {
            _targetCandidate = currentPlayer;
            return;
        }

        GameObject playerObject = GameObject.Find(_targetObjectName);
        if (playerObject == null)
        {
            _targetCandidate = null;
            return;
        }

        CharacterController characterController = playerObject.GetComponentInChildren<CharacterController>();
        if (characterController != null)
        {
            _targetCandidate = characterController.transform;
            return;
        }

        MonoBehaviour[] behaviours = playerObject.GetComponentsInChildren<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICombatDamageReceiver)
            {
                _targetCandidate = behaviours[i].transform;
                return;
            }
        }

        _targetCandidate = playerObject.transform;
    }

    private Vector3 ResolveOrigin()
    {
        return _fishboneOrigin != null
            ? _fishboneOrigin.position
            : transform.TransformPoint(_originLocalOffset);
    }

    private Vector3 ResolveTargetPoint()
    {
        if (_targetCandidate == null)
        {
            return transform.position + transform.forward * Mathf.Max(1f, _meleeAttackRange);
        }

        return _targetCandidate.position + _targetOffset;
    }

    private Vector3 ResolveAttackForward()
    {
        Vector3 origin = ResolveOrigin();
        Vector3 target = ResolveTargetPoint();
        Vector3 direction = target - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private void FaceTarget()
    {
        Vector3 direction = ResolveTargetPoint() - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Vector3[] BuildMeleePath(Vector3 origin, Vector3 forward, float sweep)
    {
        Vector3[] points = new Vector3[Mathf.Max(MinimumPathPoints, _pathPointCount)];
        float leadAngle = Mathf.Lerp(-90f, 118f, sweep);
        float trailingAngle = leadAngle - 128f;

        for (int i = 0; i < points.Length; i++)
        {
            float u = i / (float)(points.Length - 1);
            float angle = Mathf.Lerp(trailingAngle, leadAngle, u);
            float radius = Mathf.Lerp(0.35f, _meleeAttackRange, Mathf.Pow(u, 0.78f));
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            float lift = Mathf.Sin(u * Mathf.PI) * 0.34f + u * 0.08f;
            float snap = Mathf.Sin((sweep * 5.5f + u * 2.2f) * Mathf.PI) * 0.06f;
            points[i] = origin + direction * radius + Vector3.up * (lift + snap);
        }

        return points;
    }

    private Vector3[] BuildRangedPath(Vector3 origin, Vector3 endpoint, float normalizedTime)
    {
        Vector3[] points = new Vector3[Mathf.Max(MinimumPathPoints, _pathPointCount)];
        Vector3 delta = endpoint - origin;
        Vector3 forward = delta.sqrMagnitude > 0.0001f ? delta.normalized : transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        if (side.sqrMagnitude <= 0.0001f)
        {
            side = transform.right;
        }

        side.Normalize();
        float length = Mathf.Max(0.01f, delta.magnitude);
        float wave = Mathf.Clamp(length * 0.12f, 0.12f, 0.62f);

        for (int i = 0; i < points.Length; i++)
        {
            float u = i / (float)(points.Length - 1);
            float coil = Mathf.Sin((u * 2.2f + normalizedTime * 3.1f) * Mathf.PI) * wave;
            float lift = Mathf.Sin(u * Mathf.PI) * Mathf.Min(0.45f, length * 0.08f);
            points[i] = Vector3.Lerp(origin, endpoint, u) + side * coil + Vector3.up * lift;
        }

        return points;
    }

    private float BuildJawOpenAmount(float normalizedTime)
    {
        if (normalizedTime < 0.55f)
        {
            return Mathf.Lerp(0.18f, 1f, Smooth01(normalizedTime / 0.55f));
        }

        if (normalizedTime < 0.72f)
        {
            return 1f - Smooth01((normalizedTime - 0.55f) / 0.17f) * 0.9f;
        }

        if (normalizedTime < 0.84f)
        {
            return Mathf.Lerp(0.1f, 0.38f, Smooth01((normalizedTime - 0.72f) / 0.12f));
        }

        return Mathf.Lerp(0.38f, 0.12f, Smooth01((normalizedTime - 0.84f) / 0.16f));
    }

    private void ApplyMeleeDamage(Vector3 origin, Vector3 forward)
    {
        Vector3 center = origin + forward * (_meleeAttackRange * 0.62f);
        HashSet<ICombatDamageReceiver> damagedReceivers = new HashSet<ICombatDamageReceiver>();
        Collider[] hits = Physics.OverlapSphere(
            center,
            Mathf.Max(0.05f, _meleeDamageRadius),
            _damageMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            TryApplyDamage(hits[i], _meleeDamage, ResolveColliderHitPoint(hits[i], center), forward, damagedReceivers);
        }

        Transform target = _targetCandidate;
        if (target != null && Vector3.Distance(target.position, center) <= _meleeDamageRadius + 0.45f)
        {
            TryApplyDamage(target, _meleeDamage, target.position + _targetOffset, forward, damagedReceivers);
        }
    }

    private static Vector3 ResolveColliderHitPoint(Collider targetCollider, Vector3 fallbackPoint)
    {
        if (targetCollider == null)
        {
            return fallbackPoint;
        }

        MeshCollider meshCollider = targetCollider as MeshCollider;
        if (meshCollider != null && !meshCollider.convex)
        {
            return targetCollider.bounds.ClosestPoint(fallbackPoint);
        }

        return targetCollider.ClosestPoint(fallbackPoint);
    }

    private void ApplyBiteDamage(Vector3 origin, Vector3 endpoint)
    {
        HashSet<ICombatDamageReceiver> damagedReceivers = new HashSet<ICombatDamageReceiver>();
        Vector3 direction = endpoint - origin;
        float distance = direction.magnitude;
        if (distance <= 0.0001f)
        {
            direction = transform.forward;
            distance = 0.1f;
        }
        else
        {
            direction /= distance;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.05f, _biteRadius * 0.45f),
            direction,
            distance,
            _damageMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            TryApplyDamage(hits[i].collider, _biteDamage, hits[i].point, direction, damagedReceivers);
        }

        Transform target = _targetCandidate;
        if (target != null && Vector3.Distance(target.position + _targetOffset, endpoint) <= _biteRadius)
        {
            TryApplyDamage(target, _biteDamage, endpoint, direction, damagedReceivers);
        }
    }

    private bool TryApplyDamage(
        Component component,
        float damage,
        Vector3 hitPoint,
        Vector3 hitDirection,
        HashSet<ICombatDamageReceiver> damagedReceivers)
    {
        if (component == null)
        {
            return false;
        }

        if (CombatDamageUtility.TryGetDamageReceiver(component, out ICombatDamageReceiver receiver))
        {
            if (damagedReceivers.Contains(receiver))
            {
                return false;
            }

            damagedReceivers.Add(receiver);
            CombatDamageUtility.ApplyDamageTo(receiver, damage, hitPoint, hitDirection, gameObject);
            return true;
        }

        return false;
    }

    private bool TryApplyDamage(
        Transform target,
        float damage,
        Vector3 hitPoint,
        Vector3 hitDirection,
        HashSet<ICombatDamageReceiver> damagedReceivers)
    {
        if (target == null)
        {
            return false;
        }

        if (CombatDamageUtility.TryGetDamageReceiver(target, out ICombatDamageReceiver receiver) ||
            TryGetDamageReceiverInChildren(target, out receiver))
        {
            if (damagedReceivers.Contains(receiver))
            {
                return false;
            }

            damagedReceivers.Add(receiver);
            CombatDamageUtility.ApplyDamageTo(receiver, damage, hitPoint, hitDirection, gameObject);
            return true;
        }

        return false;
    }

    private static bool TryGetDamageReceiverInChildren(Transform target, out ICombatDamageReceiver receiver)
    {
        receiver = null;
        if (target == null)
        {
            return false;
        }

        MonoBehaviour[] behaviours = target.GetComponentsInChildren<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ICombatDamageReceiver candidate &&
                candidate.IsCombatDamageReceiverAlive)
            {
                receiver = candidate;
                return true;
            }
        }

        return false;
    }

    private void SpawnEnergyBurst(Vector3 position, Vector3 direction, int count, float size, float speed, float lifetime)
    {
        GameObject burstObject = new GameObject("SkeFishboneEnergyBurst");
        burstObject.transform.SetPositionAndRotation(
            position,
            direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction.normalized, Vector3.up) : Quaternion.identity);
        burstObject.layer = gameObject.layer;

        ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.18f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.45f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.25f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.45f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(_energyColor, _energyFadeColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(8, count + 4);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, short.MaxValue)) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 38f;
        shape.radius = 0.12f;
        shape.length = 0.18f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildGradient(_energyColor, _energyColor, Transparent(_energyColor)));

        ParticleSystemRenderer rendererComponent = particles.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetParticleMaterial();
        rendererComponent.sortingOrder = 20;

        particles.Play();
        Destroy(burstObject, lifetime + 0.45f);
    }

    private void EnsureRuntimeMaterials()
    {
        if (_boneMaterial != null)
        {
            return;
        }

        _boneMaterial = CreateColoredMaterial("SkeFishboneBoneMaterial", _boneColor, false);
        _shadowMaterial = CreateColoredMaterial("SkeFishboneShadowMaterial", _boneShadowColor, true);
        _energyMaterial = CreateColoredMaterial("SkeFishboneEnergyMaterial", _energyColor, true);
        _socketMaterial = CreateColoredMaterial("SkeFishboneSocketMaterial", Color.black, false);
    }

    private Material GetParticleMaterial()
    {
        if (_particleMaterial != null)
        {
            return _particleMaterial;
        }

        _particleMaterial = CreateColoredMaterial("SkeFishboneParticleMaterial", Color.white, true);
        Texture2D texture = GetSoftParticleTexture();
        if (_particleMaterial.HasProperty("_BaseMap"))
        {
            _particleMaterial.SetTexture("_BaseMap", texture);
        }

        if (_particleMaterial.HasProperty("_MainTex"))
        {
            _particleMaterial.SetTexture("_MainTex", texture);
        }

        return _particleMaterial;
    }

    private Material CreateColoredMaterial(string materialName, Color color, bool transparent)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = materialName
        };

        ApplyMaterialColor(material, color);

        if (transparent)
        {
            material.renderQueue = (int)RenderQueue.Transparent;
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }
        }

        return material;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private Mesh GetConeMesh()
    {
        if (_coneMesh != null)
        {
            return _coneMesh;
        }

        const int sides = 10;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        vertices.Add(new Vector3(0f, 0f, 1f));
        for (int i = 0; i < sides; i++)
        {
            float angle = i / (float)sides * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f));
        }

        vertices.Add(Vector3.zero);
        int centerIndex = vertices.Count - 1;

        for (int i = 0; i < sides; i++)
        {
            int current = 1 + i;
            int next = 1 + ((i + 1) % sides);
            triangles.Add(0);
            triangles.Add(next);
            triangles.Add(current);
            triangles.Add(centerIndex);
            triangles.Add(current);
            triangles.Add(next);
        }

        _coneMesh = new Mesh
        {
            name = "RuntimeFishboneCone"
        };
        _coneMesh.SetVertices(vertices);
        _coneMesh.SetTriangles(triangles, 0);
        _coneMesh.RecalculateNormals();
        _coneMesh.RecalculateBounds();
        return _coneMesh;
    }

    private static Texture2D GetSoftParticleTexture()
    {
        if (s_softParticleTexture != null)
        {
            return s_softParticleTexture;
        }

        const int textureSize = 64;
        s_softParticleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "RuntimeSkeFishboneSoftParticle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float u = (x + 0.5f) / textureSize - 0.5f;
                float v = (y + 0.5f) / textureSize - 0.5f;
                float distance = Mathf.Sqrt(u * u + v * v);
                float alpha = 1f - Mathf.SmoothStep(0.28f, 0.5f, distance);
                s_softParticleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        s_softParticleTexture.Apply(false, true);
        return s_softParticleTexture;
    }

    private static Gradient BuildGradient(Color start, Color middle, Color end)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(middle, 0.42f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(middle.a, 0.48f),
                new GradientAlphaKey(end.a, 1f)
            });
        return gradient;
    }

    private static Color Transparent(Color color)
    {
        color.a = 0f;
        return color;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
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

    private sealed class FishboneVfxInstance
    {
        private readonly SkeFishboneAttackVfx _owner;
        private readonly GameObject _rootObject;
        private readonly LineRenderer _shadowLine;
        private readonly LineRenderer _boneLine;
        private readonly LineRenderer _energyLine;
        private readonly LineRenderer _energyEchoLine;
        private readonly Transform[] _vertebrae;
        private readonly Transform[] _leftRibs;
        private readonly Transform[] _rightRibs;
        private readonly Transform _headRoot;
        private readonly Transform _upperJaw;
        private readonly Transform _lowerJaw;
        private readonly Transform[] _upperTeeth;
        private readonly Transform[] _lowerTeeth;

        public FishboneVfxInstance(SkeFishboneAttackVfx owner, string rootName, bool createHead)
        {
            _owner = owner;
            _rootObject = new GameObject(rootName);
            _rootObject.layer = owner.gameObject.layer;

            _shadowLine = CreateLine("FishboneInkShadow", owner._shadowMaterial, 0.24f, 0.08f);
            _boneLine = CreateLine("FishboneSpine", owner._boneMaterial, 0.11f, 0.04f);
            _energyLine = CreateLine("FishboneCyanTrail", owner._energyMaterial, 0.18f, 0.02f);
            _energyEchoLine = CreateLine("FishboneCyanEcho", owner._energyMaterial, 0.09f, 0.01f);

            _vertebrae = new Transform[owner._boneSegmentCount];
            _leftRibs = new Transform[owner._boneSegmentCount];
            _rightRibs = new Transform[owner._boneSegmentCount];

            for (int i = 0; i < owner._boneSegmentCount; i++)
            {
                _vertebrae[i] = CreatePrimitivePiece("FishboneVertebra", PrimitiveType.Sphere, owner._boneMaterial);
                _leftRibs[i] = CreateConePiece("FishboneLeftRib", owner._boneMaterial);
                _rightRibs[i] = CreateConePiece("FishboneRightRib", owner._boneMaterial);
            }

            if (createHead)
            {
                _headRoot = new GameObject("MutatedFishSkull").transform;
                _headRoot.SetParent(_rootObject.transform, false);

                Transform skull = CreatePrimitivePiece("SkullPlate", PrimitiveType.Sphere, owner._boneMaterial);
                skull.SetParent(_headRoot, false);
                skull.localScale = new Vector3(0.38f, 0.24f, 0.54f);
                skull.localPosition = new Vector3(0f, 0f, 0.05f);

                Transform leftSocket = CreatePrimitivePiece("LeftSocket", PrimitiveType.Sphere, owner._socketMaterial);
                leftSocket.SetParent(_headRoot, false);
                leftSocket.localPosition = new Vector3(-0.12f, 0.075f, 0.24f);
                leftSocket.localScale = new Vector3(0.09f, 0.07f, 0.06f);

                Transform rightSocket = CreatePrimitivePiece("RightSocket", PrimitiveType.Sphere, owner._socketMaterial);
                rightSocket.SetParent(_headRoot, false);
                rightSocket.localPosition = new Vector3(0.12f, 0.075f, 0.24f);
                rightSocket.localScale = new Vector3(0.09f, 0.07f, 0.06f);

                _upperJaw = new GameObject("UpperFangJaw").transform;
                _upperJaw.SetParent(_headRoot, false);
                _lowerJaw = new GameObject("LowerFangJaw").transform;
                _lowerJaw.SetParent(_headRoot, false);

                _upperTeeth = CreateTeeth(_upperJaw, true);
                _lowerTeeth = CreateTeeth(_lowerJaw, false);
            }
            else
            {
                _headRoot = null;
                _upperJaw = null;
                _lowerJaw = null;
                _upperTeeth = new Transform[0];
                _lowerTeeth = new Transform[0];
            }

            SetObjectLayerRecursive(_rootObject, owner.gameObject.layer);
        }

        public void UpdatePath(Vector3[] path, float alpha, float jawOpen, float normalizedTime)
        {
            if (path == null || path.Length < 2)
            {
                return;
            }

            ApplyLine(_shadowLine, path, _owner._boneShadowColor, _owner._boneShadowColor, alpha * 0.82f);
            ApplyLine(_boneLine, path, _owner._boneColor, _owner._boneColor, alpha);
            ApplyLine(_energyLine, OffsetPath(path, Mathf.Sin(normalizedTime * Mathf.PI * 5f) * 0.055f), _owner._energyColor, _owner._energyFadeColor, alpha);
            ApplyLine(_energyEchoLine, OffsetPath(path, Mathf.Cos(normalizedTime * Mathf.PI * 4f) * -0.09f), _owner._energyFadeColor, _owner._energyColor, alpha * 0.62f);

            for (int i = 0; i < _vertebrae.Length; i++)
            {
                float u = (i + 0.5f) / _vertebrae.Length;
                SamplePath(path, u, out Vector3 position, out Vector3 tangent);
                Vector3 right = Vector3.Cross(Vector3.up, tangent);
                if (right.sqrMagnitude <= 0.0001f)
                {
                    right = _owner.transform.right;
                }

                right.Normalize();
                Vector3 ribTilt = Vector3.down * 0.38f + tangent * 0.18f;
                float scale = Mathf.Lerp(1.18f, 0.62f, u);
                float ripple = 1f + Mathf.Sin((normalizedTime * 8f + u * 5f) * Mathf.PI) * 0.09f;

                _vertebrae[i].gameObject.SetActive(alpha > 0.03f);
                _vertebrae[i].position = position;
                _vertebrae[i].rotation = Quaternion.LookRotation(tangent, Vector3.up);
                _vertebrae[i].localScale = new Vector3(0.14f, 0.1f, 0.18f) * scale * ripple;

                UpdateRib(_leftRibs[i], position, right + ribTilt, scale, alpha);
                UpdateRib(_rightRibs[i], position, -right + ribTilt, scale, alpha);
            }

            UpdateHead(path, alpha, jawOpen);
        }

        public void Destroy()
        {
            if (_rootObject != null)
            {
                Object.Destroy(_rootObject);
            }
        }

        private LineRenderer CreateLine(string objectName, Material material, float startWidth, float endWidth)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(_rootObject.transform, false);
            lineObject.layer = _owner.gameObject.layer;

            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.enabled = true;
            lineRenderer.useWorldSpace = true;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.startWidth = startWidth;
            lineRenderer.endWidth = endWidth;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.sharedMaterial = material;
            return lineRenderer;
        }

        private Transform CreatePrimitivePiece(string objectName, PrimitiveType primitiveType, Material material)
        {
            GameObject piece = GameObject.CreatePrimitive(primitiveType);
            piece.name = objectName;
            piece.transform.SetParent(_rootObject.transform, false);
            piece.layer = _owner.gameObject.layer;

            Collider collider = piece.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            Renderer renderer = piece.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return piece.transform;
        }

        private Transform CreateConePiece(string objectName, Material material)
        {
            GameObject piece = new GameObject(objectName);
            piece.transform.SetParent(_rootObject.transform, false);
            piece.layer = _owner.gameObject.layer;

            MeshFilter meshFilter = piece.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = _owner.GetConeMesh();

            MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return piece.transform;
        }

        private Transform[] CreateTeeth(Transform parent, bool upper)
        {
            Transform[] teeth = new Transform[5];
            for (int i = 0; i < teeth.Length; i++)
            {
                Transform tooth = CreateConePiece(upper ? "UpperMutatedFang" : "LowerMutatedFang", _owner._boneMaterial);
                tooth.SetParent(parent, false);

                float x = Mathf.Lerp(-0.16f, 0.16f, i / (float)(teeth.Length - 1));
                float y = upper ? -0.065f : 0.055f;
                float z = 0.23f + Mathf.Abs(x) * -0.22f;

                tooth.localPosition = new Vector3(x, y, z);
                tooth.localRotation = upper ? Quaternion.Euler(68f, 0f, 0f) : Quaternion.Euler(-112f, 0f, 0f);
                tooth.localScale = new Vector3(0.045f, 0.045f, Mathf.Lerp(0.22f, 0.32f, 1f - Mathf.Abs(x) / 0.16f));
                teeth[i] = tooth;
            }

            return teeth;
        }

        private void UpdateRib(Transform rib, Vector3 rootPosition, Vector3 direction, float scale, float alpha)
        {
            rib.gameObject.SetActive(alpha > 0.03f);
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
            rib.position = rootPosition + normalizedDirection * 0.06f;
            rib.rotation = Quaternion.LookRotation(normalizedDirection, Vector3.up);
            rib.localScale = new Vector3(0.052f, 0.052f, 0.42f * scale);
        }

        private void UpdateHead(Vector3[] path, float alpha, float jawOpen)
        {
            if (_headRoot == null)
            {
                return;
            }

            SamplePath(path, 1f, out Vector3 headPosition, out Vector3 tangent);
            _headRoot.gameObject.SetActive(alpha > 0.03f);
            _headRoot.position = headPosition;
            _headRoot.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            _headRoot.localScale = Vector3.one * Mathf.Lerp(0.75f, 1f, alpha);

            if (_upperJaw != null)
            {
                _upperJaw.localPosition = new Vector3(0f, 0.04f, 0.18f);
                _upperJaw.localRotation = Quaternion.Euler(-jawOpen * 28f, 0f, 0f);
            }

            if (_lowerJaw != null)
            {
                _lowerJaw.localPosition = new Vector3(0f, -0.04f, 0.18f);
                _lowerJaw.localRotation = Quaternion.Euler(jawOpen * 38f, 0f, 0f);
            }
        }

        private void ApplyLine(LineRenderer lineRenderer, Vector3[] path, Color startColor, Color endColor, float alpha)
        {
            lineRenderer.positionCount = path.Length;
            lineRenderer.SetPositions(path);
            startColor.a *= alpha;
            endColor.a *= alpha;
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
            lineRenderer.enabled = alpha > 0.02f;
        }

        private Vector3[] OffsetPath(Vector3[] path, float amount)
        {
            Vector3[] result = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                float u = i / (float)(path.Length - 1);
                Vector3 tangent;
                if (i < path.Length - 1)
                {
                    tangent = path[i + 1] - path[i];
                }
                else
                {
                    tangent = path[i] - path[i - 1];
                }

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
                result[i] = path[i] + right * (amount * Mathf.Sin(u * Mathf.PI));
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
