using UnityEngine;

public sealed class EnemyAwarenessRuntimeInstaller : MonoBehaviour
{
    [SerializeField, Min(0.25f)]
    private float _installScanInterval = 1f;

    private float _scanTimer;

    private void Awake()
    {
        InstallAwarenessComponents();
    }

    private void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer < _installScanInterval)
        {
            return;
        }

        _scanTimer = 0f;
        InstallAwarenessComponents();
    }

    private static void InstallAwarenessComponents()
    {
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (!(behaviours[i] is IEnemyVisionSource))
            {
                continue;
            }

            EnsureAwarenessComponents(behaviours[i].gameObject);
        }
    }

    public static void EnsureAwarenessComponents(GameObject enemyObject)
    {
        if (enemyObject == null)
        {
            return;
        }

        if (enemyObject.GetComponent<EnemyLookController>() == null)
        {
            enemyObject.AddComponent<EnemyLookController>();
        }

        if (enemyObject.GetComponent<EnemySuspicionSensor>() == null)
        {
            enemyObject.AddComponent<EnemySuspicionSensor>();
        }

        if (enemyObject.GetComponent<EnemyPatrolAwarenessController>() == null)
        {
            enemyObject.AddComponent<EnemyPatrolAwarenessController>();
        }
    }
}
