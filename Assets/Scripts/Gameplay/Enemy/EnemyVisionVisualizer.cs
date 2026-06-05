using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
/// <summary>
/// 敌人视野扇形可视化组件。
/// 根据敌人的视野数据动态生成贴地 Mesh，并在看到玩家时切换警戒颜色。
/// </summary>
public sealed class EnemyVisionVisualizer : MonoBehaviour
{
    [SerializeField, Min(1f)]
    private float _displayDistance = 35f;

    [SerializeField, Range(8, 160)]
    private int _rayCount = 48;

    [SerializeField, Min(0.02f)]
    private float _updateInterval = 0.1f;

    [SerializeField]
    private LayerMask _groundMask = 1;

    [SerializeField]
    private Material _visionMaterial;

    [SerializeField]
    private Color _normalColor = new Color(0.12f, 0.74f, 1f, 0.22f);

    [SerializeField]
    private Color _alertColor = new Color(1f, 0.22f, 0.08f, 0.34f);

    [SerializeField, Min(0.1f)]
    private float _groundProbeHeight = 12f;

    [SerializeField, Min(0f)]
    private float _groundProbeDepth = 24f;

    [SerializeField, Min(0.001f)]
    private float _groundOffset = 0.035f;

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private IEnemyVisionSource _visionSource;
    private Transform _playerTransform;
    private Vector3[] _vertices;
    private int[] _triangles;
    private Color[] _colors;
    private float _updateTimer;
    private bool _isVisible;

    private void Awake()
    {
        _meshFilter = GetComponent<MeshFilter>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _mesh = new Mesh
        {
            name = $"{name}_EnemyVisionMesh"
        };
        _mesh.MarkDynamic();
        _meshFilter.sharedMesh = _mesh;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.sharedMaterial = ResolveMaterial();
        ResolveVisionSource();
        SetVisible(false);
    }

    private void OnEnable()
    {
        _updateTimer = _updateInterval;
    }

    private void LateUpdate()
    {
        if (_visionSource == null)
        {
            ResolveVisionSource();
        }

        if (_visionSource == null)
        {
            SetVisible(false);
            return;
        }

        _playerTransform = _visionSource.PlayerTransform != null
            ? _visionSource.PlayerTransform
            : ResolvePlayerTransform();

        bool shouldDisplay = ShouldDisplayVision();
        SetVisible(shouldDisplay);
        if (!shouldDisplay)
        {
            return;
        }

        _updateTimer += Time.deltaTime;
        if (_updateTimer < _updateInterval)
        {
            return;
        }

        _updateTimer = 0f;
        RebuildVisionMesh();
    }

    /// <summary>
    /// 配置视野可视化使用的材质、地面层、显示距离和刷新频率。
    /// </summary>
    /// <param name="visionMaterial">可选视野材质。</param>
    /// <param name="groundMask">地面投影层。</param>
    /// <param name="displayDistance">距离玩家多远以内才显示扇形。</param>
    /// <param name="rayCount">扇形边界采样射线数量。</param>
    /// <param name="updateInterval">Mesh 重建间隔。</param>
    public void Configure(
        Material visionMaterial,
        LayerMask groundMask,
        float displayDistance,
        int rayCount,
        float updateInterval)
    {
        if (visionMaterial != null)
        {
            _visionMaterial = visionMaterial;
        }

        _groundMask = groundMask.value != 0 ? groundMask : _groundMask;
        _displayDistance = Mathf.Max(1f, displayDistance);
        _rayCount = Mathf.Clamp(rayCount, 8, 160);
        _updateInterval = Mathf.Max(0.02f, updateInterval);

        if (_meshRenderer != null)
        {
            _meshRenderer.sharedMaterial = ResolveMaterial();
        }
    }

    private bool ShouldDisplayVision()
    {
        if (!EnemyVisionDisplayController.IsVisionVisible ||
            !_visionSource.ShouldShowVision ||
            _playerTransform == null)
        {
            return false;
        }

        Vector3 toPlayer = _playerTransform.position - transform.position;
        toPlayer.y = 0f;
        return toPlayer.sqrMagnitude <= _displayDistance * _displayDistance;
    }

    private void RebuildVisionMesh()
    {
        int segmentCount = Mathf.Max(2, _rayCount);
        int vertexCount = segmentCount + 2;
        EnsureMeshBuffers(vertexCount, segmentCount);

        Transform sourceTransform = _visionSource.VisionTransform != null
            ? _visionSource.VisionTransform
            : transform;

        Vector3 center = ProjectToGround(sourceTransform.position);
        _vertices[0] = transform.InverseTransformPoint(center);

        float viewAngle = Mathf.Clamp(_visionSource.ViewAngle, 1f, 360f);
        float startAngle = viewAngle >= 359.9f ? -180f : -viewAngle * 0.5f;
        float step = viewAngle >= 359.9f
            ? 360f / segmentCount
            : viewAngle / segmentCount;

        // 以 VisionPivot 为中心按角度采样，遇到遮挡就把扇形边缘截断到命中点。
        for (int i = 0; i <= segmentCount; i++)
        {
            float angle = startAngle + step * i;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * sourceTransform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = sourceTransform.forward;
            }

            direction.Normalize();
            Vector3 endpoint = ResolveVisionEndpoint(sourceTransform, direction);
            Vector3 groundPoint = ProjectToGround(endpoint);
            _vertices[i + 1] = transform.InverseTransformPoint(groundPoint);
        }

        Color meshColor = _visionSource.CanSeePlayerForVision ? _alertColor : _normalColor;
        for (int i = 0; i < _colors.Length; i++)
        {
            _colors[i] = meshColor;
        }

        _mesh.Clear();
        _mesh.vertices = _vertices;
        _mesh.triangles = _triangles;
        _mesh.colors = _colors;
        _mesh.RecalculateBounds();
    }

    private Vector3 ResolveVisionEndpoint(Transform sourceTransform, Vector3 planarDirection)
    {
        Vector3 origin = EnemyVisionUtility.GetEyePosition(sourceTransform, _visionSource.EyeHeight);
        float range = Mathf.Max(0.1f, _visionSource.DetectionRange);
        Ray ray = new Ray(origin, planarDirection);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            range,
            _visionSource.LineOfSightBlockMask,
            QueryTriggerInteraction.Ignore);

        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, CompareHitDistance);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null ||
                    hitCollider.transform.IsChildOf(sourceTransform) ||
                    (_playerTransform != null && hitCollider.transform.IsChildOf(_playerTransform)))
                {
                    continue;
                }

                return hits[i].point;
            }
        }

        return origin + planarDirection * range;
    }

    private Vector3 ProjectToGround(Vector3 worldPosition)
    {
        Vector3 rayOrigin = worldPosition + Vector3.up * _groundProbeHeight;
        float rayDistance = Mathf.Max(_groundProbeHeight + _groundProbeDepth, _groundProbeHeight + 1f);
        LayerMask effectiveGroundMask = _visionSource != null && _visionSource.GroundMask.value != 0
            ? _visionSource.GroundMask
            : _groundMask;

        // 视野 Mesh 贴近地表绘制，避免在起伏地形上漂浮或穿地过多。
        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                effectiveGroundMask,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * _groundOffset;
        }

        return new Vector3(worldPosition.x, transform.position.y + _groundOffset, worldPosition.z);
    }

    private void EnsureMeshBuffers(int vertexCount, int segmentCount)
    {
        if (_vertices == null || _vertices.Length != vertexCount)
        {
            _vertices = new Vector3[vertexCount];
            _colors = new Color[vertexCount];
        }

        int triangleIndexCount = segmentCount * 3;
        if (_triangles != null && _triangles.Length == triangleIndexCount)
        {
            return;
        }

        _triangles = new int[triangleIndexCount];
        int index = 0;
        for (int i = 0; i < segmentCount; i++)
        {
            _triangles[index++] = 0;
            _triangles[index++] = i + 1;
            _triangles[index++] = i + 2;
        }
    }

    private Material ResolveMaterial()
    {
        if (_visionMaterial != null)
        {
            return _visionMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        Material material = new Material(shader)
        {
            name = "RuntimeEnemyVisionCone"
        };
        material.color = Color.white;
        _visionMaterial = material;
        return _visionMaterial;
    }

    private void ResolveVisionSource()
    {
        _visionSource = GetComponent<IEnemyVisionSource>();
        if (_visionSource != null)
        {
            return;
        }

        _visionSource = GetComponentInParent<IEnemyVisionSource>();
    }

    private Transform ResolvePlayerTransform()
    {
        if (PlayerHealthController.Instance != null)
        {
            return PlayerHealthController.Instance.transform;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }

    private void SetVisible(bool isVisible)
    {
        if (_isVisible == isVisible)
        {
            return;
        }

        _isVisible = isVisible;
        if (_meshRenderer != null)
        {
            _meshRenderer.enabled = isVisible;
        }
    }

    private static int CompareHitDistance(RaycastHit left, RaycastHit right)
    {
        return left.distance.CompareTo(right.distance);
    }
}
