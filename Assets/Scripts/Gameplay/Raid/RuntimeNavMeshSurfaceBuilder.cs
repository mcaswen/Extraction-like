using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 为运行时拼装的白盒场景统一构建导航网格数据
/// </summary>
[DefaultExecutionOrder(-250)]
[RequireComponent(typeof(NavMeshSurface))]
public class RuntimeNavMeshSurfaceBuilder : MonoBehaviour
{
    public static RuntimeNavMeshSurfaceBuilder Instance { get; private set; }

    [Header("Runtime Build")]
    public bool BuildOnStart = true;
    public float InitialBuildDelay = 0.1f;
    public bool RebuildOnRequest = true;
    public float MinimumRebuildInterval = 0.5f;

    [Header("Surface Defaults")]
    public bool ForceCollectAllObjects = true;
    public LayerMask LayerMask = ~0;
    public NavMeshCollectGeometry Geometry = NavMeshCollectGeometry.PhysicsColliders;

    private NavMeshSurface _surface;
    private bool _isBuilding;
    private bool _hasBuilt;
    private bool _started;
    private Coroutine _initialBuild;
    private float _lastBuildTime = -999f;
    public bool IsBuilding => _isBuilding;
    public bool HasPendingBuild => isActiveAndEnabled && BuildOnStart && !_hasBuilt;
    public long NavigationRevision { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _surface = GetComponent<NavMeshSurface>();
        ApplySurfaceDefaults();
    }

    private void Start()
    {
        _started = true;
        ScheduleInitialBuild();
    }

    private void OnEnable() { if (_started) ScheduleInitialBuild(); }
    private void OnDisable()
    {
        if (_initialBuild != null) StopCoroutine(_initialBuild);
        _initialBuild = null;
    }
    private void ScheduleInitialBuild()
    {
        if (BuildOnStart && !_hasBuilt && _initialBuild == null) _initialBuild = StartCoroutine(BuildAfterDelay());
    }

    private IEnumerator BuildAfterDelay()
    {
        if (InitialBuildDelay > 0f)
        {
            yield return new WaitForSeconds(InitialBuildDelay);
        }

        _initialBuild = null;
        if (!_hasBuilt) BuildNow();
    }

    /// <summary>
    /// 请求按冷却限制重建当前场景导航网格
    /// </summary>
    public void RequestRebuild()
    {
        if (!RebuildOnRequest)
        {
            return;
        }

        if (_isBuilding)
        {
            return;
        }

        if (Time.unscaledTime - _lastBuildTime < MinimumRebuildInterval)
        {
            return;
        }

        BuildNow();
    }

    /// <summary>
    /// 立即应用导航网格表面默认参数并同步构建导航网格
    /// </summary>
    public void BuildNow()
    {
        if (_initialBuild != null) StopCoroutine(_initialBuild);
        _initialBuild = null;
        if (_surface == null)
        {
            _surface = GetComponent<NavMeshSurface>();
            if (_surface == null)
            {
                _surface = gameObject.AddComponent<NavMeshSurface>();
            }
        }

        ApplySurfaceDefaults();
        _isBuilding = true;
        try
        {
            _surface.BuildNavMesh();
            _lastBuildTime = Time.unscaledTime;
            _hasBuilt = true;
            NavigationRevision++;
        }
        finally { _isBuilding = false; }
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void ApplySurfaceDefaults()
    {
        if (_surface == null)
        {
            return;
        }

        // 白盒场景通常由编辑器工具临时拼装，收集全场对象比局部体积更稳定
        if (ForceCollectAllObjects)
        {
            _surface.collectObjects = CollectObjects.All;
        }

        _surface.layerMask = LayerMask;
        _surface.useGeometry = Geometry;
    }
}
