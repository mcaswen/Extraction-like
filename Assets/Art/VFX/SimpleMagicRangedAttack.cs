using UnityEngine;

[DisallowMultipleComponent]
public sealed class SimpleMagicRangedAttack : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private bool _enableKeyboardPreview = true;
    [SerializeField] private KeyCode _castKey = KeyCode.Mouse1;
    [SerializeField] private float _cooldownSeconds = 0.28f;

    [Header("Aim")]
    [SerializeField] private Camera _aimCamera;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Vector3 _fallbackFireLocalOffset = new Vector3(0.25f, 0.85f, 0.45f);
    [SerializeField] private bool _faceAimWhenCasting = true;
    [SerializeField] private float _maxAimDistance = 80f;

    [Header("Projectile")]
    [SerializeField] private float _projectileSpeed = 13f;
    [SerializeField] private float _projectileLifetime = 1.8f;
    [SerializeField] private float _projectileCollisionRadius = 0.16f;
    [SerializeField] private LayerMask _impactMask = ~0;

    [Header("Visual")]
    [SerializeField] private Color _coreColor = new Color(0.55f, 0.9f, 1f, 1f);
    [SerializeField] private Color _rimColor = new Color(0.86f, 0.97f, 1f, 0.9f);
    [SerializeField] private Color _sparkColor = Color.white;
    [SerializeField] private float _impactScale = 1f;

    private Camera _cachedMainCamera;
    private float _cooldownRemaining;
    private Material _coreMaterial;
    private Material _rimMaterial;
    private Material _sparkMaterial;
    private static Texture2D s_softCircleTexture;

    private void Awake()
    {
        _cachedMainCamera = Camera.main;
    }

    private void OnDisable()
    {
        DestroyRuntimeMaterial(ref _coreMaterial);
        DestroyRuntimeMaterial(ref _rimMaterial);
        DestroyRuntimeMaterial(ref _sparkMaterial);
    }

    private void Update()
    {
        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.deltaTime);
        }

        if (!_enableKeyboardPreview || _cooldownRemaining > 0f || !Input.GetKeyDown(_castKey))
        {
            return;
        }

        Cast();
        _cooldownRemaining = Mathf.Max(0.01f, _cooldownSeconds);
    }

    private void Cast()
    {
        Vector3 firePosition = ResolveFirePosition();
        Vector3 direction = ResolveAimDirection(firePosition);

        if (_faceAimWhenCasting)
        {
            FaceDirection(direction);
        }

        SpawnCastFlash(firePosition, direction);
        SpawnProjectile(
            firePosition,
            direction,
            transform,
            _projectileSpeed,
            _projectileLifetime,
            _projectileCollisionRadius,
            _impactMask);
    }

    public void SetKeyboardPreviewEnabled(bool enabled)
    {
        _enableKeyboardPreview = enabled;
    }

    public bool PlayExternalCastVisual(
        Vector3 firePosition,
        Vector3 direction,
        Transform owner,
        float projectileSpeed,
        float projectileLifetime)
    {
        return PlayExternalCastVisual(
            firePosition,
            direction,
            owner,
            projectileSpeed,
            projectileLifetime,
            _projectileCollisionRadius,
            _impactMask);
    }

    public bool PlayExternalCastVisual(
        Vector3 firePosition,
        Vector3 direction,
        Transform owner,
        float projectileSpeed,
        float projectileLifetime,
        float projectileCollisionRadius,
        LayerMask impactMask)
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        Vector3 safeDirection = direction;
        safeDirection.y = 0f;
        if (safeDirection.sqrMagnitude <= 0.0001f)
        {
            safeDirection = transform.forward;
            safeDirection.y = 0f;
        }

        if (safeDirection.sqrMagnitude <= 0.0001f)
        {
            safeDirection = Vector3.forward;
        }

        safeDirection.Normalize();
        SpawnCastFlash(firePosition, safeDirection);
        SpawnProjectile(
            firePosition,
            safeDirection,
            owner != null ? owner : transform,
            projectileSpeed,
            projectileLifetime,
            projectileCollisionRadius,
            impactMask);
        return true;
    }

    private Vector3 ResolveFirePosition()
    {
        if (_firePoint != null)
        {
            return _firePoint.position;
        }

        return transform.TransformPoint(_fallbackFireLocalOffset);
    }

    private Vector3 ResolveAimDirection(Vector3 firePosition)
    {
        Camera cameraToUse = _aimCamera != null ? _aimCamera : _cachedMainCamera;
        if (cameraToUse == null)
        {
            cameraToUse = Camera.main;
            _cachedMainCamera = cameraToUse;
        }

        if (cameraToUse != null)
        {
            Ray aimRay = cameraToUse.ScreenPointToRay(Input.mousePosition);
            Plane firePlane = new Plane(Vector3.up, firePosition);
            if (firePlane.Raycast(aimRay, out float rayDistance) && rayDistance <= _maxAimDistance)
            {
                Vector3 aimPoint = aimRay.GetPoint(rayDistance);
                Vector3 aimDirection = aimPoint - firePosition;
                aimDirection.y = 0f;
                if (aimDirection.sqrMagnitude > 0.0001f)
                {
                    return aimDirection.normalized;
                }
            }
        }

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
        Vector3 planarDirection = direction;
        planarDirection.y = 0f;
        if (planarDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
    }

    private void SpawnProjectile(
        Vector3 firePosition,
        Vector3 direction,
        Transform owner,
        float projectileSpeed,
        float projectileLifetime,
        float projectileCollisionRadius,
        LayerMask impactMask)
    {
        GameObject projectileObject = new GameObject("SimpleMagicProjectile");
        projectileObject.transform.SetPositionAndRotation(
            firePosition,
            Quaternion.LookRotation(direction, Vector3.up));

        ParticleSystem core = projectileObject.AddComponent<ParticleSystem>();
        ConfigureProjectileCore(core);

        ParticleSystem trail = CreateChildParticleSystem(projectileObject.transform, "MagicTrail");
        ConfigureProjectileTrail(trail);

        SimpleMagicProjectileRuntime runtime = projectileObject.AddComponent<SimpleMagicProjectileRuntime>();
        runtime.Initialize(
            this,
            owner != null ? owner : transform,
            direction,
            projectileSpeed,
            projectileLifetime,
            projectileCollisionRadius,
            impactMask);
    }

    private void SpawnCastFlash(Vector3 firePosition, Vector3 direction)
    {
        GameObject flashObject = new GameObject("MagicCastFlash");
        flashObject.transform.SetPositionAndRotation(
            firePosition,
            Quaternion.LookRotation(direction, Vector3.up));

        ParticleSystem flash = flashObject.AddComponent<ParticleSystem>();
        PrepareParticleSystem(flash);

        ParticleSystem.MainModule main = flash.main;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(_coreColor, _sparkColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 36;

        ParticleSystem.EmissionModule emission = flash.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)24) });

        ParticleSystem.ShapeModule shape = flash.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;
        shape.radius = 0.18f;
        shape.length = 0.25f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = flash.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            BuildGradient(_sparkColor, _coreColor, Transparent(_rimColor)));

        ParticleSystemRenderer rendererComponent = flash.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetSparkMaterial();
        rendererComponent.sortingOrder = 8;

        flash.Play();
        Destroy(flashObject, 0.65f);
    }

    internal void SpawnImpactBurst(Vector3 position, Vector3 normal)
    {
        GameObject impactObject = new GameObject("MagicImpactBurst");
        impactObject.transform.position = position;
        if (normal.sqrMagnitude > 0.0001f)
        {
            impactObject.transform.rotation = Quaternion.LookRotation(normal.normalized, Vector3.up);
        }

        ParticleSystem burst = impactObject.AddComponent<ParticleSystem>();
        ConfigureImpactBurst(burst);

        ParticleSystem sparks = CreateChildParticleSystem(impactObject.transform, "MagicImpactSparks");
        ConfigureImpactSparks(sparks);

        burst.Play();
        sparks.Play();
        Destroy(impactObject, 1.25f);
    }

    private void ConfigureProjectileCore(ParticleSystem particleSystem)
    {
        PrepareParticleSystem(particleSystem);

        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startColor = new ParticleSystem.MinMaxGradient(_coreColor, _rimColor);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 72;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 55f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            BuildGradient(_coreColor, _rimColor, Transparent(_coreColor)));

        ParticleSystemRenderer rendererComponent = particleSystem.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetCoreMaterial();
        rendererComponent.sortingOrder = 8;

        particleSystem.Play();
    }

    private void ConfigureProjectileTrail(ParticleSystem particleSystem)
    {
        PrepareParticleSystem(particleSystem);

        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.44f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(_rimColor, Transparent(_coreColor));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 78f;

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.06f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            BuildGradient(_rimColor, _coreColor, Transparent(_rimColor)));

        ParticleSystemRenderer rendererComponent = particleSystem.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetRimMaterial();
        rendererComponent.sortingOrder = 7;

        particleSystem.Play();
    }

    private void ConfigureImpactBurst(ParticleSystem particleSystem)
    {
        PrepareParticleSystem(particleSystem);

        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.52f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f * _impactScale, 2.7f * _impactScale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f * _impactScale, 0.36f * _impactScale);
        main.startColor = new ParticleSystem.MinMaxGradient(_coreColor, _rimColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)42) });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f * _impactScale;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            BuildGradient(_sparkColor, _coreColor, Transparent(_rimColor)));

        ParticleSystemRenderer rendererComponent = particleSystem.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetCoreMaterial();
        rendererComponent.sortingOrder = 10;
    }

    private void ConfigureImpactSparks(ParticleSystem particleSystem)
    {
        PrepareParticleSystem(particleSystem);

        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 0.32f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.1f * _impactScale, 4.8f * _impactScale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f * _impactScale, 0.07f * _impactScale);
        main.startColor = new ParticleSystem.MinMaxGradient(_sparkColor, _coreColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)28) });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f * _impactScale;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            BuildGradient(_sparkColor, _coreColor, Transparent(_sparkColor)));

        ParticleSystemRenderer rendererComponent = particleSystem.GetComponent<ParticleSystemRenderer>();
        rendererComponent.renderMode = ParticleSystemRenderMode.Billboard;
        rendererComponent.sharedMaterial = GetSparkMaterial();
        rendererComponent.sortingOrder = 11;
    }

    private ParticleSystem CreateChildParticleSystem(Transform parent, string objectName)
    {
        GameObject childObject = new GameObject(objectName);
        childObject.transform.SetParent(parent, false);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        return childObject.AddComponent<ParticleSystem>();
    }

    private Material GetCoreMaterial()
    {
        return GetOrCreateParticleMaterial(ref _coreMaterial, _coreColor);
    }

    private Material GetRimMaterial()
    {
        return GetOrCreateParticleMaterial(ref _rimMaterial, _rimColor);
    }

    private Material GetSparkMaterial()
    {
        return GetOrCreateParticleMaterial(ref _sparkMaterial, _sparkColor);
    }

    private static Material GetOrCreateParticleMaterial(ref Material material, Color tint)
    {
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        material = new Material(shader);
        material.name = "RuntimeMagicParticle";
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        ApplySoftCircleTexture(material);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", tint);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", tint);
        }

        return material;
    }

    private static void ApplySoftCircleTexture(Material material)
    {
        Texture2D texture = GetSoftCircleTexture();
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_ColorMode"))
        {
            material.SetFloat("_ColorMode", 0f);
        }
    }

    private static Texture2D GetSoftCircleTexture()
    {
        if (s_softCircleTexture != null)
        {
            return s_softCircleTexture;
        }

        const int textureSize = 64;
        const float edgeStart = 0.42f;
        const float edgeEnd = 0.5f;

        s_softCircleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "RuntimeSoftCircleParticle",
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
                float alpha = 1f - Mathf.SmoothStep(edgeStart, edgeEnd, distance);
                s_softCircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        s_softCircleTexture.Apply(false, true);
        return s_softCircleTexture;
    }

    private static void PrepareParticleSystem(ParticleSystem particleSystem)
    {
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
                new GradientAlphaKey(middle.a, 0.42f),
                new GradientAlphaKey(end.a, 1f)
            });
        return gradient;
    }

    private static Color Transparent(Color color)
    {
        color.a = 0f;
        return color;
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
}

internal sealed class SimpleMagicProjectileRuntime : MonoBehaviour
{
    private SimpleMagicRangedAttack _caster;
    private Transform _owner;
    private Vector3 _direction;
    private float _speed;
    private float _lifeRemaining;
    private float _collisionRadius;
    private LayerMask _impactMask;

    public void Initialize(
        SimpleMagicRangedAttack caster,
        Transform owner,
        Vector3 direction,
        float speed,
        float lifetime,
        float collisionRadius,
        LayerMask impactMask)
    {
        _caster = caster;
        _owner = owner;
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        _speed = Mathf.Max(0f, speed);
        _lifeRemaining = Mathf.Max(0.05f, lifetime);
        _collisionRadius = Mathf.Max(0.01f, collisionRadius);
        _impactMask = impactMask;
    }

    private void Update()
    {
        float distance = _speed * Time.deltaTime;
        if (distance > 0f && TryFindImpact(distance, out RaycastHit hitInfo))
        {
            transform.position = hitInfo.point;
            _caster?.SpawnImpactBurst(hitInfo.point, hitInfo.normal);
            Destroy(gameObject);
            return;
        }

        transform.position += _direction * distance;
        _lifeRemaining -= Time.deltaTime;
        if (_lifeRemaining <= 0f)
        {
            _caster?.SpawnImpactBurst(transform.position, -_direction);
            Destroy(gameObject);
        }
    }

    private bool TryFindImpact(float distance, out RaycastHit nearestHit)
    {
        nearestHit = default;
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            _collisionRadius,
            _direction,
            distance,
            _impactMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, CompareHitDistance);
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || ShouldIgnore(hit.collider))
            {
                continue;
            }

            nearestHit = hit;
            return true;
        }

        return false;
    }

    private bool ShouldIgnore(Collider hitCollider)
    {
        if (hitCollider == null || hitCollider.isTrigger || _owner == null)
        {
            return true;
        }

        Transform hitTransform = hitCollider.transform;
        return hitTransform == _owner ||
               hitTransform.IsChildOf(_owner) ||
               _owner.IsChildOf(hitTransform);
    }

    private static int CompareHitDistance(RaycastHit a, RaycastHit b)
    {
        return a.distance.CompareTo(b.distance);
    }
}
