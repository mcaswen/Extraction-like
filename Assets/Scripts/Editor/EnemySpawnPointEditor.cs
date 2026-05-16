using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemySpawnPoint))]
public sealed class EnemySpawnPointEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EnemySpawnPoint spawnPoint = (EnemySpawnPoint)target;
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Assign Child Routes"))
            {
                Undo.RecordObject(spawnPoint, "Assign Child Patrol Routes");
                spawnPoint.CollectChildRoutes();
                EditorUtility.SetDirty(spawnPoint);
            }

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Spawn All Now"))
                {
                    spawnPoint.SpawnAll();
                }
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Spawn All Now is available in Play Mode to avoid editing the scene accidentally.", MessageType.Info);
        }
    }
}
