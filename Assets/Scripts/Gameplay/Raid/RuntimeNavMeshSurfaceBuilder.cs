using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds scene NavMesh data at runtime for procedurally assembled whitebox scenes.
/// This should be used scene-wide instead of rebuilding per enemy spawn.
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
    private float _lastBuildTime = -999f;

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
        if (BuildOnStart)
        {
            StartCoroutine(BuildAfterDelay());
        }
    }

    private IEnumerator BuildAfterDelay()
    {
        if (InitialBuildDelay > 0f)
        {
            yield return new WaitForSeconds(InitialBuildDelay);
        }

        BuildNow();
    }

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

    public void BuildNow()
    {
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
        _surface.BuildNavMesh();
        _lastBuildTime = Time.unscaledTime;
        _isBuilding = false;
    }

    private void ApplySurfaceDefaults()
    {
        if (_surface == null)
        {
            return;
        }

        if (ForceCollectAllObjects)
        {
            _surface.collectObjects = CollectObjects.All;
        }

        _surface.layerMask = LayerMask;
        _surface.useGeometry = Geometry;
    }
}
