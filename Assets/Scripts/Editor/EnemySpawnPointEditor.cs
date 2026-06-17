using UnityEditor;
using UnityEngine;

/// <summary>
/// 敌人出生点组件的检视面板扩展
/// </summary>
[CustomEditor(typeof(EnemySpawnPoint))]
public sealed class EnemySpawnPointEditor : Editor
{
    private SerializedProperty _enemyPrefab;
    private SerializedProperty _patrolRoute;

    private void OnEnable()
    {
        _enemyPrefab = serializedObject.FindProperty("_enemyPrefab");
        _patrolRoute = serializedObject.FindProperty("_patrolRoute");
    }

    /// <summary>
    /// 绘制敌人出生点检视面板，并提示旧配置兜底用途
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(
            "敌人预制体现在配在父级 Enemy Source Cluster 上。这里仅作为旧配置兜底。",
            MessageType.Info);
        EditorGUILayout.PropertyField(_enemyPrefab, new GUIContent("旧版敌人预制体兜底"));
        EditorGUILayout.PropertyField(_patrolRoute);
        serializedObject.ApplyModifiedProperties();
    }
}
