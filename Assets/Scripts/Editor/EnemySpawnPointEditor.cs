using UnityEditor;

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
        EditorGUILayout.PropertyField(_enemyPrefab);
        EditorGUILayout.PropertyField(_patrolRoute);
        serializedObject.ApplyModifiedProperties();
    }
}
