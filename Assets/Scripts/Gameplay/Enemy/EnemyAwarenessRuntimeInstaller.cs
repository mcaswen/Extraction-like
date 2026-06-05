using UnityEngine;

/// <summary>
/// 敌人感知组件运行时安装器。
/// 用于给实现视野接口的敌人自动补齐看向、听觉和巡逻感知组件。
/// </summary>
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

    /// <summary>
    /// 确保指定敌人对象拥有巡逻感知所需的运行时组件。
    /// </summary>
    /// <param name="enemyObject">需要安装感知组件的敌人对象。</param>
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
