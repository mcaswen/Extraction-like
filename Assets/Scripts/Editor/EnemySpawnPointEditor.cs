using UnityEditor;
using UnityEngine;

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
