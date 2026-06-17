using UnityEditor;
using UnityEngine;

/// <summary>
/// 敌人巡逻路线组件的检视面板扩展
/// </summary>
[CustomEditor(typeof(EnemyPatrolRoute))]
public sealed class EnemyPatrolRouteEditor : Editor
{
    /// <summary>
    /// 绘制巡逻路线检视面板，并提供一键创建或同步路点的按钮
    /// </summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EnemyPatrolRoute route = (EnemyPatrolRoute)target;
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Collect Child Waypoints"))
            {
                Undo.RecordObject(route, "Collect Patrol Route Waypoints");
                route.CollectChildWaypoints();
                EditorUtility.SetDirty(route);
            }

            if (GUILayout.Button("Clear Missing"))
            {
                Undo.RecordObject(route, "Clear Missing Patrol Waypoints");
                route.ClearMissingWaypoints();
                EditorUtility.SetDirty(route);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reverse Route"))
            {
                Undo.RecordObject(route, "Reverse Patrol Route");
                route.ReverseWaypoints();
                EditorUtility.SetDirty(route);
            }

            if (GUILayout.Button("Validate Route"))
            {
                int assigned = route.CountAssignedWaypoints();
                int sampled = route.CountSampledWaypoints(route.DefaultNavMeshSampleRadius);
                string message = $"Assigned waypoints: {assigned}\nNavMesh-sampled waypoints: {sampled}";
                if (sampled < 2)
                {
                    message += "\n\nRoute is not usable until at least two waypoints can be sampled on the NavMesh.";
                }

                EditorUtility.DisplayDialog("Validate Patrol Route", message, "OK");
            }
        }
    }

    private void OnSceneGUI()
    {
        EnemyPatrolRoute route = (EnemyPatrolRoute)target;
        if (route == null || route.Waypoints == null)
        {
            return;
        }

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = Color.white },
            alignment = TextAnchor.MiddleCenter
        };

        for (int i = 0; i < route.Waypoints.Count; i++)
        {
            EnemyPatrolWaypoint waypoint = route.Waypoints[i];
            if (waypoint == null || waypoint.Point == null)
            {
                continue;
            }

            Handles.Label(waypoint.Point.position + Vector3.up * 0.45f, i.ToString(), labelStyle);
        }
    }
}
