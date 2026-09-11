using System.Collections;
using System.Collections.Generic;
using Gameplay.Agent.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class ZombieTentacleCorrosionVfx : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform _preferredTarget;
    [SerializeField] private string[] _targetNameHints = { "Ai Idle", "Agent", "AI" };
    [SerializeField] private Transform _playerDamageTarget;
    [SerializeField] private string _playerObjectName = "Player";
    [SerializeField] private LayerMask _targetMask = ~0;

    [Header("Tentacle Attack")]
    [SerializeField] private bool _autoAttackNearbyTarget = true;
    [SerializeField] private float _initialDelay = 0.7f;
    [SerializeField] private float _attackInterval = 2.4f;
    [SerializeField] private float _pullRange = 5.2f;
    [SerializeField] private float _attachDuration = 0.24f;
    [SerializeField] private float _pullDuration = 1.05f;
    [SerializeField] private float _pullStrength = 7f;
    [SerializeField] private float _holdDistanceFromZombie = 1.15f;
    [SerializeField] private float _targetAttachHeight = 0.85f;
    [SerializeField] private float _originHeight = 1.08f;
    [SerializeField] private float _originForwardOffset = 0.38f;
    [SerializeField] private bool _allowKeyboardPreview = true;
    [SerializeField] private KeyCode _previewAttackKey = KeyCode.H;

    [Header("Corrosion Slime")]
    [SerializeField] private float _slimeRadius = 2.35f;
    [SerializeField] private float _slimeDuration = 6f;
    [SerializeField] private float _slimeDamagePerSecond = 8f;
    [SerializeField] private float _slimeDamageTickInterval = 0.25f;
    [SerializeField] private float _slimeGroundOffset = 0.035f;

    [Header("Visual")]
    [SerializeField] private int _tentaclePointCount = 18;
    [SerializeField] private int _slimeRingPointCount = 72;
    [SerializeField] private Color _tentacleColor = new Color(0.03f, 0.08f, 0.055f, 1f);
    [SerializeField] private Color _tentacleRimColor = new Color(0.55f, 0.95f, 0.48f, 0.72f);
    [SerializeField] private Color _slimeColor = new Color(0.015f, 0.03f, 0.02f, 0.82f);
    [SerializeField] private Color _slimeRingColor = new Color(0.76f, 1f, 0.6f, 0.9f);
    [SerializeField] private Color _splashColor = new Color(0.32f, 0.9f, 0.38f, 0.72f);

    private const int MinimumTentaclePoints = 6;
    private const int MinimumSlimeRingPoints = 24;
    private const int MaxTargetHits = 24;
    private const float TargetRefreshInterval = 0.35f;

    private static readonly Collider[] s_targetHits = new Collider[MaxTargetHits];
    private static Texture2D s_softParticleTexture;

    private readonly List<SlimePoolInstance> _activeSlimePools = new List<SlimePoolInstance>();

    private Material _tentacleMaterial;
    private Material _tentacleRimMaterial;
    private Material _slimeDiskMaterial;
    private Material _slimeLineMaterial;
    private Material _particleMaterial;

    private Coroutine _attackRoutine;
    private TentacleInstance _activeTentacleVisual;
    private Transform _cachedPlayerTarget;
    private Transform _tentacleOriginOverride;
    private float _nextAttackTimer;
    private float _targetRefreshTimer;
    private float _noiseSeed;
    private float _enemyDrivenVisualDuration = -1f;
    private bool _suppressGameplayEffects;

    private void Awake()
    {
        _tentaclePointCount = Mathf.Max(MinimumTentaclePoints, _tentaclePointCount);
        _slimeRingPointCount = Mathf.Max(MinimumSlimeRingPoints, _slimeRingPointCount);
        _nextAttackTimer = Mathf.Max(0f, _initialDelay);
        _noiseSeed = Random.Range(0f, 100f);
        EnsureRuntimeMaterials();
    }

    private void OnDisable()
    {
        if (_attackRoutine != null)
        {
            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
        }

        ClearActiveTentacleVisual();

        for (int i = _activeSlimePools.Count - 1; i >= 0; i--)
        {
            _activeSlimePools[i].Destroy();
        }

        _activeSlimePools.Clear();

        DestroyRuntimeMaterial(ref _tentacleMaterial);
        DestroyRuntimeMaterial(ref _tentacleRimMaterial);
        DestroyRuntimeMaterial(ref _slimeDiskMaterial);
        DestroyRuntimeMaterial(ref _slimeLineMaterial);
        DestroyRuntimeMaterial(ref _particleMaterial);
    }

    private void Update()
    {
        TickSlimePools();

        if (_allowKeyboardPreview && Input.GetKeyDown(_previewAttackKey))
        {
            TriggerTentacleAttack();
            return;
        }

        if (_attackRoutine != null || !_autoAttackNearbyTarget)
        {
            return;
        }

        _nextAttackTimer -= Time.deltaTime;
        if (_nextAttackTimer > 0f)
        {
            return;
        }

        Transform target = FindNearbyTarget();
        if (target != null)
        {
            TriggerTentacleAttack(target);
        }
        else
        {
            _nextAttackTimer = 0.2f;
        }
    }

    public void TriggerTentacleAttack()
    {
        TriggerTentacleAttack(FindNearbyTarget(true), !_suppressGameplayEffects, true);
    }

    public void TriggerTentacleAttack(Transform target)
    {
        TriggerTentacleAttack(target, !_suppressGameplayEffects, true);
    }

    public void ConfigureAsEnemyDrivenVisual(Transform preferredTarget)
    {
        ConfigureAsEnemyDrivenVisual(preferredTarget, null, -1f);
    }

    public void ConfigureAsEnemyDrivenVisual(Transform preferredTarget, Transform tentacleOrigin, float visualDuration)
    {
        _preferredTarget = preferredTarget;
        _tentacleOriginOverride = tentacleOrigin;
        _enemyDrivenVisualDuration = visualDuration;
        _autoAttackNearbyTarget = false;
        _allowKeyboardPreview = false;
        _suppressGameplayEffects = true;
        enabled = true;
    }

    public void PlayTentacleAttackVisual(Transform target)
    {
        TriggerTentacleAttack(target != null ? target : _preferredTarget, false, false, true);
    }

    public void PlaySlimePoolVisual(Vector3 position, float radius, float duration)
    {
        EnsureRuntimeMaterials();
        SpawnSlimePool(ProjectToGround(position), radius, duration, false);
    }

    private void TriggerTentacleAttack(Transform target, bool applyGameplayEffects, bool spawnSlimePool)
    {
        TriggerTentacleAttack(target, applyGameplayEffects, spawnSlimePool, false);
    }

    private void TriggerTentacleAttack(Transform target, bool applyGameplayEffects, bool spawnSlimePool, bool restartActiveVisual)
    {
        if (_attackRoutine != null || target == null)
        {
            if (!restartActiveVisual || target == null)
            {
                return;
            }

            StopCoroutine(_attackRoutine);
            _attackRoutine = null;
            ClearActiveTentacleVisual();
        }

        if (!isActiveAndEnabled)
        {
            return;
        }

        _nextAttackTimer = Mathf.Max(0.2f, _attackInterval);
        _attackRoutine = StartCoroutine(PlayTentacleAttack(target, applyGameplayEffects, spawnSlimePool));
    }

    private IEnumerator PlayTentacleAttack(Transform target, bool applyGameplayEffects, bool spawnSlimePool)
    {
        PullTargetHandle targetHandle = new PullTargetHandle(target);
        TentacleInstance tentacle = new TentacleInstance(this);
        _activeTentacleVisual = tentacle;
        Vector3 slimePosition = ProjectToGround(targetHandle.Position);
        bool slimeCreated = false;

        float attachDuration = Mathf.Max(0.05f, _attachDuration);
        float timer = 0f;
        while (timer < attachDuration && targetHandle.IsValid)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / attachDuration);
            float extension = Smooth01(normalized);
            Vector3 origin = ResolveTentacleOrigin();
            Vector3 attachPoint = ResolveTargetAttachPoint(targetHandle);
            Vector3 endpoint = Vector3.Lerp(origin, attachPoint, extension);
            tentacle.UpdatePath(BuildTentaclePath(origin, endpoint, normalized), 1f, normalized);
            yield return null;
        }

        if (spawnSlimePool && targetHandle.IsValid)
        {
            slimeCreated = true;
            slimePosition = ProjectToGround(targetHandle.Position);
            SpawnSlimePool(slimePosition, _slimeRadius, _slimeDuration, applyGameplayEffects);
        }

        float pullDuration = Mathf.Max(0.05f, _suppressGameplayEffects && _enemyDrivenVisualDuration > 0f
            ? _enemyDrivenVisualDuration
            : _pullDuration);
        timer = 0f;
        while (timer < pullDuration && targetHandle.IsValid)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / pullDuration);
            if (applyGameplayEffects)
            {
                PullTargetTowardZombie(targetHandle);
            }

            Vector3 origin = ResolveTentacleOrigin();
            Vector3 attachPoint = ResolveTargetAttachPoint(targetHandle);
            tentacle.UpdatePath(BuildTentaclePath(origin, attachPoint, normalized + 1f), 1f - normalized * 0.12f, normalized + 1f);

            if (spawnSlimePool && !slimeCreated && normalized > 0.1f)
            {
                slimeCreated = true;
                slimePosition = ProjectToGround(targetHandle.Position);
                SpawnSlimePool(slimePosition, _slimeRadius, _slimeDuration, applyGameplayEffects);
            }

            yield return null;
        }

        float fadeDuration = 0.22f;
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float normalized = Mathf.Clamp01(timer / fadeDuration);
            Vector3 origin = ResolveTentacleOrigin();
            Vector3 endpoint = targetHandle.IsValid ? ResolveTargetAttachPoint(targetHandle) : slimePosition + Vector3.up * _targetAttachHeight;
            tentacle.UpdatePath(BuildTentaclePath(origin, endpoint, normalized + 2f), 1f - normalized, normalized + 2f);
            yield return null;
        }

        if (_activeTentacleVisual == tentacle)
        {
            _activeTentacleVisual = null;
        }

        tentacle.Destroy();
        _attackRoutine = null;
    }

    private void PullTargetTowardZombie(PullTargetHandle targetHandle)
    {
        Vector3 targetPosition = targetHandle.Position;
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            toTarget = ResolveForward();
        }

        Vector3 desiredPosition = transform.position + toTarget.normalized * Mathf.Max(0.2f, _holdDistanceFromZombie);
        desiredPosition.y = targetPosition.y;
        Vector3 delta = desiredPosition - targetPosition;
        float maxStep = Mathf.Max(0.1f, _pullStrength) * Time.deltaTime;
        if (delta.magnitude > maxStep)
        {
            delta = delta.normalized * maxStep;
        }

        targetHandle.Move(delta);
    }

    private Transform FindNearbyTarget(bool forceRefresh = false)
    {
        if (_preferredTarget != null && IsValidTarget(_preferredTarget))
        {
            float preferredDistance = Vector3.Distance(transform.position, _preferredTarget.position);
            if (preferredDistance <= _pullRange)
            {
                return ResolvePullRoot(_preferredTarget);
            }
        }

        _targetRefreshTimer -= Time.deltaTime;
        if (!forceRefresh && _targetRefreshTimer > 0f)
        {
            return null;
        }

        _targetRefreshTimer = TargetRefreshInterval;

        Transform bestTarget = null;
        float bestDistanceSqr = _pullRange * _pullRange;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _pullRange, s_targetHits, _targetMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider candidateCollider = s_targetHits[i];
            s_targetHits[i] = null;
            if (candidateCollider == null)
            {
                continue;
            }

            Transform candidate = ResolvePullRoot(candidateCollider.transform);
            TryAssignBestTarget(candidate, ref bestTarget, ref bestDistanceSqr);
        }

        if (bestTarget != null)
        {
            return bestTarget;
        }

        Transform[] sceneTransforms = FindObjectsOfType<Transform>();
        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform candidate = sceneTransforms[i];
            if (!IsNameHintTarget(candidate))
            {
                continue;
            }

            TryAssignBestTarget(ResolvePullRoot(candidate), ref bestTarget, ref bestDistanceSqr);
        }

        return bestTarget;
    }

    private void TryAssignBestTarget(Transform candidate, ref Transform bestTarget, ref float bestDistanceSqr)
    {
        if (!IsValidTarget(candidate))
        {
            return;
        }

        Vector3 delta = candidate.position - transform.position;
        float distanceSqr = delta.sqrMagnitude;
        if (distanceSqr > bestDistanceSqr)
        {
            return;
        }

        bestDistanceSqr = distanceSqr;
        bestTarget = candidate;
    }

    private Transform ResolvePullRoot(Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        AgentPawnRoot agentPawnRoot = candidate.GetComponentInParent<AgentPawnRoot>();
        if (agentPawnRoot != null)
        {
            return agentPawnRoot.transform;
        }

        CharacterController characterController = candidate.GetComponentInParent<CharacterController>();
        if (characterController != null)
        {
            return characterController.transform;
        }

        return candidate;
    }

    private bool IsValidTarget(Transform candidate)
    {
        if (candidate == null || candidate == transform || candidate.IsChildOf(transform))
        {
            return false;
        }

        if (!candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        return IsNameHintTarget(candidate) ||
               candidate.GetComponentInParent<AgentPawnRoot>() != null ||
               candidate.GetComponentInParent<CharacterController>() != null ||
               CombatDamageUtility.TryGetDamageReceiver(candidate, out _);
    }

    private bool IsNameHintTarget(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (_targetNameHints == null || _targetNameHints.Length == 0)
        {
            return false;
        }

        string candidateName = candidate.name;
        for (int i = 0; i < _targetNameHints.Length; i++)
        {
            string hint = _targetNameHints[i];
            if (!string.IsNullOrWhiteSpace(hint) &&
                candidateName.IndexOf(hint, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 ResolveTentacleOrigin()
    {
        if (_tentacleOriginOverride != null)
        {
            return _tentacleOriginOverride.position;
        }

        return transform.position + Vector3.up * _originHeight + ResolveForward() * _originForwardOffset;
    }

    private void ClearActiveTentacleVisual()
    {
        if (_activeTentacleVisual == null)
        {
            return;
        }

        _activeTentacleVisual.Destroy();
        _activeTentacleVisual = null;
    }

    private Vector3 ResolveTargetAttachPoint(PullTargetHandle targetHandle)
    {
        return targetHandle.Position + Vector3.up * _targetAttachHeight;
    }

    private Vector3 ResolveForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        return forward.normalized;
    }

    private Vector3[] BuildTentaclePath(Vector3 origin, Vector3 endpoint, float phase)
    {
        Vector3 delta = endpoint - origin;
        Vector3 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : ResolveForward();
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = transform.right;
        }

        right.Normalize();
        Vector3 up = Vector3.Cross(direction, right).normalized;
        Vector3[] points = new Vector3[_tentaclePointCount];
        for (int i = 0; i < points.Length; i++)
        {
            float u = i / (float)(points.Length - 1);
            float envelope = Mathf.Sin(u * Mathf.PI);
            float whip = Mathf.Sin(u * Mathf.PI * 3.2f + phase * 5.8f + _noiseSeed) * 0.14f;
            float sag = Mathf.Sin(u * Mathf.PI) * -0.18f;
            points[i] = Vector3.Lerp(origin, endpoint, u) + right * whip * envelope + up * sag;
        }

        return points;
    }

    private void SpawnSlimePool(Vector3 position)
    {
        SpawnSlimePool(position, _slimeRadius, _slimeDuration, !_suppressGameplayEffects);
    }

    private void SpawnSlimePool(Vector3 position, float radius, float duration, bool applyDamage)
    {
        SlimePoolInstance slimePool = new SlimePoolInstance(this, position, radius, duration, applyDamage);
        _activeSlimePools.Add(slimePool);
    }

    private void TickSlimePools()
    {
        Transform playerTarget = ResolvePlayerDamageTarget();
        for (int i = _activeSlimePools.Count - 1; i >= 0; i--)
        {
            if (_activeSlimePools[i].Tick(Time.deltaTime, playerTarget))
            {
                _activeSlimePools[i].Destroy();
                _activeSlimePools.RemoveAt(i);
            }
        }
    }

    private Transform ResolvePlayerDamageTarget()
    {
        if (_playerDamageTarget != null)
        {
            return _playerDamageTarget;
        }

        if (_cachedPlayerTarget != null)
        {
            return _cachedPlayerTarget;
        }

        if (PlayerTargetResolver.TryGetCurrentPlayerTransform(transform, out Transform currentPlayer))
        {
            _cachedPlayerTarget = currentPlayer;
            return _cachedPlayerTarget;
        }

        if (!string.IsNullOrWhiteSpace(_playerObjectName))
        {
            GameObject namedPlayer = GameObject.Find(_playerObjectName);
            if (namedPlayer != null)
            {
                _cachedPlayerTarget = namedPlayer.transform;
                return _cachedPlayerTarget;
            }
        }

        return null;
    }

    private Vector3 ProjectToGround(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * 1.2f;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 8f, _targetMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.normal.y > 0.35f && hit.point.y <= position.y + 0.15f)
            {
                return hit.point + Vector3.up * _slimeGroundOffset;
            }
        }

        float fallbackY = Mathf.Min(position.y, transform.position.y);
        return new Vector3(position.x, fallbackY + _slimeGroundOffset, position.z);
    }

    private void ApplySlimeDamage(Transform playerTarget, Vector3 center, float radius, float damage)
    {
        if (playerTarget == null || damage <= 0f)
        {
            return;
        }

        Vector3 playerPosition = playerTarget.position;
        Vector2 planarPlayer = new Vector2(playerPosition.x, playerPosition.z);
        Vector2 planarCenter = new Vector2(center.x, center.z);
        if (Vector2.Distance(planarPlayer, planarCenter) > radius)
        {
            return;
        }

        Vector3 direction = playerPosition - center;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.up;
        }

        if (CombatDamageUtility.TryGetDamageReceiver(playerTarget, out ICombatDamageReceiver receiver))
        {
            CombatDamageUtility.ApplyDamageTo(receiver, damage, playerPosition, direction.normalized, gameObject);
            return;
        }

    }

    private void EnsureRuntimeMaterials()
    {
        if (_tentacleMaterial != null)
        {
            return;
        }

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null)
        {
            spriteShader = Shader.Find("Unlit/Transparent");
        }

        Shader colorShader = Shader.Find("Unlit/Color");
        if (colorShader == null)
        {
            colorShader = spriteShader;
        }

        _tentacleMaterial = CreateMaterial("Runtime Zombie Tentacle", spriteShader, _tentacleColor, null);
        _tentacleRimMaterial = CreateMaterial("Runtime Zombie Tentacle Rim", spriteShader, _tentacleRimColor, null);
        _slimeDiskMaterial = CreateMaterial("Runtime Zombie Corrosion Disk", colorShader, _slimeColor, null);
        _slimeLineMaterial = CreateMaterial("Runtime Zombie Corrosion Lines", spriteShader, _slimeRingColor, null);
        _particleMaterial = CreateMaterial("Runtime Zombie Corrosion Particles", spriteShader, _splashColor, GetSoftParticleTexture());
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
            name = "Runtime Zombie Soft Corrosion Particle",
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

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
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

    private sealed class PullTargetHandle
    {
        private readonly Transform _target;
        private readonly CharacterController _characterController;
        private readonly NavMeshAgent _navMeshAgent;

        public PullTargetHandle(Transform target)
        {
            _target = target;
            if (_target != null)
            {
                _characterController = _target.GetComponent<CharacterController>();
                _navMeshAgent = _target.GetComponent<NavMeshAgent>();
            }
        }

        public bool IsValid => _target != null && _target.gameObject.activeInHierarchy;
        public Vector3 Position => _target != null ? _target.position : Vector3.zero;

        public void Move(Vector3 delta)
        {
            if (!IsValid || delta.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            if (_navMeshAgent != null && _navMeshAgent.enabled)
            {
                _navMeshAgent.Warp(_target.position + delta);
                return;
            }

            if (_characterController != null && _characterController.enabled)
            {
                _characterController.Move(delta);
                return;
            }

            _target.position += delta;
        }
    }

    private sealed class TentacleInstance
    {
        private readonly ZombieTentacleCorrosionVfx _owner;
        private readonly GameObject _rootObject;
        private readonly LineRenderer _shadowLine;
        private readonly LineRenderer _bodyLine;
        private readonly LineRenderer _rimLine;
        private readonly Transform[] _suctionCups;

        public TentacleInstance(ZombieTentacleCorrosionVfx owner)
        {
            _owner = owner;
            _rootObject = new GameObject("ZombieTentacleSuctionVfx");
            _rootObject.layer = owner.gameObject.layer;

            _shadowLine = CreateLine("TentacleInkMass", owner._tentacleMaterial, 0.34f, 0);
            _bodyLine = CreateLine("TentacleBody", owner._tentacleMaterial, 0.2f, 1);
            _rimLine = CreateLine("TentacleCorrosionRim", owner._tentacleRimMaterial, 0.055f, 2);

            _suctionCups = new Transform[9];
            for (int i = 0; i < _suctionCups.Length; i++)
            {
                GameObject cup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cup.name = "TentacleSuctionCup";
                cup.layer = owner.gameObject.layer;
                cup.transform.SetParent(_rootObject.transform, false);
                Collider collider = cup.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.Destroy(collider);
                }

                Renderer renderer = cup.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = owner._tentacleRimMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                _suctionCups[i] = cup.transform;
            }
        }

        public void UpdatePath(Vector3[] path, float alpha, float phase)
        {
            if (path == null || path.Length < 2)
            {
                return;
            }

            ApplyLine(_shadowLine, path, _owner._tentacleColor, _owner._tentacleColor, alpha * 0.85f, 0.34f);
            ApplyLine(_bodyLine, path, _owner._tentacleColor, _owner._tentacleColor, alpha, 0.2f);
            ApplyLine(_rimLine, OffsetPath(path, 0.07f * Mathf.Sin(phase * 5f)), _owner._tentacleRimColor, _owner._splashColor, alpha * 0.8f, 0.055f);
            UpdateSuctionCups(path, alpha);
        }

        public void Destroy()
        {
            if (_rootObject != null)
            {
                Object.Destroy(_rootObject);
            }
        }

        private LineRenderer CreateLine(string objectName, Material material, float width, int sortingOrder)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(_rootObject.transform, false);
            lineObject.layer = _owner.gameObject.layer;

            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 8;
            lineRenderer.numCornerVertices = 8;
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
            lineRenderer.widthMultiplier = width;
            lineRenderer.enabled = alpha > 0.02f;
        }

        private void UpdateSuctionCups(Vector3[] path, float alpha)
        {
            for (int i = 0; i < _suctionCups.Length; i++)
            {
                Transform cup = _suctionCups[i];
                float u = Mathf.Lerp(0.18f, 0.86f, i / (float)(_suctionCups.Length - 1));
                SamplePath(path, u, out Vector3 position, out Vector3 tangent);
                Vector3 right = Vector3.Cross(Vector3.up, tangent);
                if (right.sqrMagnitude <= 0.0001f)
                {
                    right = _owner.transform.right;
                }

                right.Normalize();
                float side = i % 2 == 0 ? 1f : -1f;
                cup.gameObject.SetActive(alpha > 0.05f);
                cup.position = position + right * side * 0.105f + Vector3.down * 0.03f;
                cup.localScale = Vector3.one * Mathf.Lerp(0.09f, 0.055f, u) * Mathf.Clamp01(alpha);
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

    private sealed class SlimePoolInstance
    {
        private readonly ZombieTentacleCorrosionVfx _owner;
        private readonly GameObject _rootObject;
        private readonly LineRenderer _outerRingLine;
        private readonly LineRenderer _innerRingLine;
        private readonly LineRenderer _rippleLine;
        private readonly ParticleSystem _splashParticles;
        private readonly Vector3 _center;
        private readonly float _radius;
        private readonly float _duration;
        private readonly bool _applyDamage;
        private readonly Mesh _diskMesh;

        private float _elapsed;
        private float _damageTickTimer;

        public SlimePoolInstance(ZombieTentacleCorrosionVfx owner, Vector3 center, float radius, float duration, bool applyDamage)
        {
            _owner = owner;
            _center = center;
            _radius = Mathf.Max(0.25f, radius);
            _duration = Mathf.Max(0.05f, duration);
            _applyDamage = applyDamage;

            _rootObject = new GameObject("ZombieCorrosiveSlimePool");
            _rootObject.layer = owner.gameObject.layer;
            _rootObject.transform.position = center;

            GameObject diskObject = new GameObject("SlimePoolDarkDisk");
            diskObject.layer = owner.gameObject.layer;
            diskObject.transform.SetParent(_rootObject.transform, false);
            MeshFilter meshFilter = diskObject.AddComponent<MeshFilter>();
            _diskMesh = BuildDiskMesh(owner._slimeRingPointCount, _radius);
            meshFilter.sharedMesh = _diskMesh;

            MeshRenderer renderer = diskObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = owner._slimeDiskMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _outerRingLine = CreateRingLine("SlimeOuterBurnRing", 0.06f, 3);
            _innerRingLine = CreateRingLine("SlimeInnerRipples", 0.042f, 4);
            _rippleLine = CreateRingLine("SlimeWhiteCorrosionRipple", 0.035f, 5);
            _splashParticles = CreateSplashParticles();
            _splashParticles.Emit(42);
        }

        public bool Tick(float deltaTime, Transform playerTarget)
        {
            _elapsed += deltaTime;
            float normalized = Mathf.Clamp01(_elapsed / _duration);
            float alpha = 1f - Mathf.SmoothStep(0.78f, 1f, normalized);

            UpdateRings(alpha);
            TickDamage(deltaTime, playerTarget);
            return _elapsed >= _duration;
        }

        public void Destroy()
        {
            if (_rootObject != null)
            {
                Object.Destroy(_rootObject);
            }

            if (_diskMesh != null)
            {
                Object.Destroy(_diskMesh);
            }
        }

        private void TickDamage(float deltaTime, Transform playerTarget)
        {
            if (!_applyDamage)
            {
                return;
            }

            _damageTickTimer -= deltaTime;
            if (_damageTickTimer > 0f)
            {
                return;
            }

            float tickInterval = Mathf.Max(0.05f, _owner._slimeDamageTickInterval);
            _damageTickTimer = tickInterval;
            _owner.ApplySlimeDamage(playerTarget, _center, _radius, _owner._slimeDamagePerSecond * tickInterval);
        }

        private void UpdateRings(float alpha)
        {
            float time = Time.time;
            ApplyRing(_outerRingLine, _radius, 0.09f, time * 0.9f, _owner._slimeRingColor, _owner._splashColor, alpha);
            ApplyRing(_innerRingLine, _radius * 0.68f, 0.11f, -time * 1.25f, _owner._splashColor, Color.white, alpha * 0.78f);
            ApplyRing(_rippleLine, _radius * (0.32f + Mathf.PingPong(time * 0.16f, 0.22f)), 0.055f, time * 2.2f, Color.white, _owner._slimeRingColor, alpha * 0.72f);
        }

        private LineRenderer CreateRingLine(string objectName, float width, int sortingOrder)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.layer = _owner.gameObject.layer;
            lineObject.transform.SetParent(_rootObject.transform, false);

            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.widthMultiplier = width;
            lineRenderer.sharedMaterial = _owner._slimeLineMaterial;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.sortingOrder = sortingOrder;
            return lineRenderer;
        }

        private ParticleSystem CreateSplashParticles()
        {
            GameObject particleObject = new GameObject("SlimeSplashParticles");
            particleObject.layer = _owner.gameObject.layer;
            particleObject.transform.SetParent(_rootObject.transform, false);
            particleObject.transform.localPosition = Vector3.up * 0.04f;

            ParticleSystem particleSystem = particleObject.AddComponent<ParticleSystem>();
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(_owner._splashColor, Color.white);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 12f;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = _radius * 0.96f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(_owner._splashColor, 0.42f),
                    new GradientColorKey(_owner._slimeRingColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.75f, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _owner._particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 6;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particleSystem.Play();
            return particleSystem;
        }

        private void ApplyRing(LineRenderer lineRenderer, float radius, float wobble, float phase, Color startColor, Color endColor, float alpha)
        {
            int count = Mathf.Max(MinimumSlimeRingPoints, _owner._slimeRingPointCount);
            Vector3[] points = new Vector3[count + 1];
            for (int i = 0; i < points.Length; i++)
            {
                float u = i / (float)count;
                float angle = u * Mathf.PI * 2f;
                float wave = Mathf.Sin(angle * 5f + phase) * wobble + Mathf.Cos(angle * 9f - phase * 0.7f) * wobble * 0.45f;
                float finalRadius = Mathf.Max(0.05f, radius + wave);
                points[i] = _center + new Vector3(Mathf.Cos(angle) * finalRadius, 0.02f, Mathf.Sin(angle) * finalRadius);
            }

            startColor.a *= alpha;
            endColor.a *= alpha;
            lineRenderer.positionCount = points.Length;
            lineRenderer.SetPositions(points);
            lineRenderer.startColor = startColor;
            lineRenderer.endColor = endColor;
            lineRenderer.enabled = alpha > 0.02f;
        }

        private static Mesh BuildDiskMesh(int segments, float radius)
        {
            segments = Mathf.Max(MinimumSlimeRingPoints, segments);
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            Color[] colors = new Color[vertices.Length];

            vertices[0] = Vector3.zero;
            colors[0] = Color.white;
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                colors[i + 1] = Color.white;
            }

            for (int i = 0; i < segments; i++)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i == segments - 1 ? 1 : i + 2;
            }

            Mesh mesh = new Mesh
            {
                name = "Runtime Zombie Corrosion Disk"
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
