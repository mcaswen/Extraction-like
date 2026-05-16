using UnityEngine;

public sealed class EnemyVisionRuntimeInstaller : MonoBehaviour
{
    [SerializeField]
    private Material _visionMaterial;

    [SerializeField]
    private LayerMask _groundMask = 1;

    [SerializeField, Min(1f)]
    private float _displayDistance = 35f;

    [SerializeField, Range(8, 160)]
    private int _rayCount = 48;

    [SerializeField, Min(0.02f)]
    private float _updateInterval = 0.1f;

    [SerializeField, Min(0.25f)]
    private float _installScanInterval = 1f;

    private float _scanTimer;

    private void Awake()
    {
        _ = EnemyVisionDisplayController.Instance;
        InstallVisionVisualizers();
    }

    private void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer < _installScanInterval)
        {
            return;
        }

        _scanTimer = 0f;
        InstallVisionVisualizers();
    }

    private void InstallVisionVisualizers()
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IEnemyVisionSource visionSource)
            {
                continue;
            }

            Transform sourceTransform = visionSource.VisionTransform;
            if (sourceTransform == null)
            {
                continue;
            }

            EnemyVisionVisualizer visualizer = sourceTransform.GetComponentInChildren<EnemyVisionVisualizer>(true);
            if (visualizer == null)
            {
                visualizer = behaviours[i].GetComponentInChildren<EnemyVisionVisualizer>(true);
                if (visualizer != null && visualizer.transform.parent != sourceTransform)
                {
                    visualizer.transform.SetParent(sourceTransform, false);
                }
            }

            if (visualizer == null)
            {
                GameObject visualizerObject = new GameObject("EnemyVisionVisualizer");
                visualizerObject.transform.SetParent(sourceTransform, false);
                visualizer = visualizerObject.AddComponent<EnemyVisionVisualizer>();
            }

            visualizer.Configure(
                _visionMaterial,
                _groundMask,
                _displayDistance,
                _rayCount,
                _updateInterval);
        }
    }
}
