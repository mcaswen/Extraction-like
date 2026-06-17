#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public sealed class SceneNavMeshSurfaceBakeTool : EditorWindow
{
    private const string SourceScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final.unity";
    private const string SourceAssetPath = "Assets/Scenes/Scene_DB/Scenezl_Final/NavMesh-NavMesh Surface.asset";
    private const string TargetScenePath = "Assets/Scenes/Scene_DB/Scenezl_Final 1.unity";
    private const string TargetAssetPath = "Assets/Scenes/Scene_DB/Scenezl_Final 1/NavMesh-NavMesh Surface.asset";
    private const string SurfaceName = "NavMesh Surface";

    private NavMeshSurface targetSurface;
    private NavMeshSurface sourceSurfacePreview;

    [MenuItem("Tools/NavMesh/Scene NavMesh Align Tool")]
    private static void Open()
    {
        var window = GetWindow<SceneNavMeshSurfaceBakeTool>("NavMesh Align");
        window.minSize = new Vector2(520f, 220f);
        window.RefreshSelection();
    }

    [MenuItem("Tools/NavMesh/Apply Scenezl_Final NavMesh To Active Scene")]
    private static void ApplySourceNavMeshToActiveScene()
    {
        ApplySourceNavMeshToTarget(EditorSceneManager.GetActiveScene());
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Active Scene", EditorSceneManager.GetActiveScene().path);
        EditorGUILayout.LabelField("Source Scene", SourceScenePath);
        EditorGUILayout.LabelField("Source Asset", SourceAssetPath);
        EditorGUILayout.LabelField("Target Asset", TargetAssetPath);
        EditorGUILayout.Space(6f);

        using (new EditorGUILayout.HorizontalScope())
        {
            targetSurface = (NavMeshSurface)EditorGUILayout.ObjectField("Target Surface", targetSurface, typeof(NavMeshSurface), true);
            if (GUILayout.Button("Find", GUILayout.Width(64f)))
                RefreshSelection();
        }

        DrawSurfacePreview();

        using (new EditorGUI.DisabledScope(targetSurface == null))
        {
            EditorGUILayout.Space(8f);

            if (GUILayout.Button("Apply Source NavMesh And Align Surface", GUILayout.Height(32f)))
                ApplySourceNavMeshToTarget(EditorSceneManager.GetActiveScene(), targetSurface);
        }
    }

    private void RefreshSelection()
    {
        var selected = Selection.GetFiltered<NavMeshSurface>(SelectionMode.Editable)
            .FirstOrDefault(s => s.gameObject.scene == EditorSceneManager.GetActiveScene());
        targetSurface = selected != null ? selected : FindDefaultSurface(EditorSceneManager.GetActiveScene());
        sourceSurfacePreview = TryFindLoadedSourceSurface();
        Repaint();
    }

    private void DrawSurfacePreview()
    {
        if (sourceSurfacePreview == null)
        {
            EditorGUILayout.HelpBox("Source scene preview is only shown when Scenezl_Final is loaded. The Apply button can still load it temporarily to read the source surface transform.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("Source Surface Position", FormatVector(sourceSurfacePreview.transform.position));
            EditorGUILayout.LabelField("Source Surface Rotation", sourceSurfacePreview.transform.rotation.eulerAngles.ToString("F3"));
        }

        if (targetSurface != null)
        {
            EditorGUILayout.LabelField("Current Target Position", FormatVector(targetSurface.transform.position));
            EditorGUILayout.LabelField("Current Target Rotation", targetSurface.transform.rotation.eulerAngles.ToString("F3"));
        }
    }

    private static void ApplySourceNavMeshToTarget(Scene activeScene, NavMeshSurface explicitTargetSurface = null)
    {
        if (activeScene.path != TargetScenePath)
        {
            var confirm = EditorUtility.DisplayDialog(
                "NavMesh Align",
                $"Active scene is not the expected target scene.\n\nActive:\n{activeScene.path}\n\nExpected:\n{TargetScenePath}\n\nContinue anyway?",
                "Continue",
                "Cancel");
            if (!confirm)
                return;
        }

        var target = explicitTargetSurface != null ? explicitTargetSurface : FindDefaultSurface(activeScene);
        if (target == null)
        {
            EditorUtility.DisplayDialog("NavMesh Align", "No NavMeshSurface found in the active scene.", "OK");
            return;
        }

        if (!File.Exists(SourceAssetPath))
        {
            EditorUtility.DisplayDialog("NavMesh Align", $"Missing source NavMesh asset:\n{SourceAssetPath}", "OK");
            return;
        }

        if (!File.Exists(TargetAssetPath))
        {
            EditorUtility.DisplayDialog("NavMesh Align", $"Missing target NavMesh asset:\n{TargetAssetPath}", "OK");
            return;
        }

        var openedSourceScene = false;
        var sourceScene = SceneManager.GetSceneByPath(SourceScenePath);

        try
        {
            if (!sourceScene.IsValid() || !sourceScene.isLoaded)
            {
                sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
                openedSourceScene = true;
            }

            var source = FindDefaultSurface(sourceScene);
            if (source == null)
                throw new IOException($"No NavMeshSurface named '{SurfaceName}' was found in source scene: {SourceScenePath}");

            Undo.RecordObject(target.transform, "Align NavMesh Surface Transform");
            Undo.RecordObject(target, "Apply Source NavMesh Data");

            target.RemoveData();
            FileUtil.ReplaceFile(SourceAssetPath, TargetAssetPath);
            AssetDatabase.ImportAsset(TargetAssetPath, ImportAssetOptions.ForceUpdate);

            var targetData = AssetDatabase.LoadAssetAtPath<NavMeshData>(TargetAssetPath);
            if (targetData == null)
                throw new IOException($"Failed to load target NavMeshData after applying source asset: {TargetAssetPath}");

            target.transform.position = source.transform.position;
            target.transform.rotation = source.transform.rotation;
            target.navMeshData = targetData;

            if (target.isActiveAndEnabled)
                target.AddData();

            EditorUtility.SetDirty(target.transform);
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[SceneNavMeshSurfaceBakeTool] Applied source NavMesh '{SourceAssetPath}' to '{TargetAssetPath}' and aligned target surface to source surface position {FormatVector(source.transform.position)}.",
                target);
            EditorUtility.DisplayDialog(
                "NavMesh Align",
                $"Applied source NavMesh and aligned target surface.\n\nTarget asset:\n{TargetAssetPath}\n\nTarget surface position:\n{FormatVector(target.transform.position)}",
                "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("NavMesh Align", exception.Message, "OK");
        }
        finally
        {
            if (openedSourceScene)
                EditorSceneManager.CloseScene(sourceScene, true);
        }
    }

    private static NavMeshSurface TryFindLoadedSourceSurface()
    {
        var sourceScene = SceneManager.GetSceneByPath(SourceScenePath);
        return sourceScene.IsValid() && sourceScene.isLoaded ? FindDefaultSurface(sourceScene) : null;
    }

    private static NavMeshSurface FindDefaultSurface(Scene scene)
    {
        var surfaces = Resources.FindObjectsOfTypeAll<NavMeshSurface>()
            .Where(s => s != null && s.gameObject.scene == scene && !EditorUtility.IsPersistent(s))
            .ToArray();

        if (surfaces.Length == 0)
            return null;

        return surfaces.FirstOrDefault(s => s.gameObject.name == "NavMesh Surface")
            ?? surfaces.FirstOrDefault(s => Selection.gameObjects.Contains(s.gameObject))
            ?? (surfaces.Length == 1 ? surfaces[0] : null);
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F3}, {value.y:F3}, {value.z:F3})";
    }
}
#endif
