using UnityEngine;

/// <summary>
/// 敌人视野运行时引导器。
/// 场景加载后确保视野显示和感知安装器存在。
/// </summary>
public static class EnemyVisionBootstrapper
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureEnemyVisionRuntime()
    {
        if (Object.FindObjectOfType<EnemyVisionRuntimeInstaller>() != null &&
            Object.FindObjectOfType<EnemyAwarenessRuntimeInstaller>() != null)
        {
            return;
        }

        GameObject runtimeObject = GameObject.Find("EnemyVisionRuntime");
        if (runtimeObject == null)
        {
            runtimeObject = new GameObject("EnemyVisionRuntime");
        }

        if (runtimeObject.GetComponent<EnemyVisionRuntimeInstaller>() == null)
        {
            runtimeObject.AddComponent<EnemyVisionRuntimeInstaller>();
        }

        if (runtimeObject.GetComponent<EnemyAwarenessRuntimeInstaller>() == null)
        {
            runtimeObject.AddComponent<EnemyAwarenessRuntimeInstaller>();
        }
    }
}
