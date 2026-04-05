using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneLylSupportMigrator
{
    private const string SourceScenePath = "Assets/Scenes/Scene_lyl.unity";

    [MenuItem("Tools/Whitebox/Import Core Gameplay Support From Scene_lyl")]
    private static void ImportCoreGameplaySupportFromSceneLyl()
    {
        Scene targetScene = SceneManager.GetActiveScene();
        if (!targetScene.IsValid() || string.Equals(targetScene.path, SourceScenePath, System.StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Import Core Gameplay Support",
                "Please open the target whitebox scene first.",
                "OK");
            return;
        }

        Scene sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Additive);
        try
        {
            ImportInventorySupport(sourceScene, targetScene);
            ImportPromptSupport(sourceScene, targetScene);
            ImportEventSystem(sourceScene, targetScene);
            ImportOrConfigureCamera(sourceScene, targetScene);
            EnsurePlayerInteractionConfigured(sourceScene, targetScene);
            EnsureRuntimeBinderConfigured(targetScene);

            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorUtility.DisplayDialog(
                "Import Core Gameplay Support",
                "Camera follow, inventory support and prompt UI have been imported from Scene_lyl, and runtime player auto-binding has been enabled.",
                "OK");
        }
        finally
        {
            EditorSceneManager.CloseScene(sourceScene, true);
        }
    }

    private static void ImportInventorySupport(Scene sourceScene, Scene targetScene)
    {
        InventoryScreenController sourceInventory = FindComponentInScene<InventoryScreenController>(sourceScene);
        if (sourceInventory == null)
        {
            return;
        }

        InventoryScreenController targetInventory = FindComponentInScene<InventoryScreenController>(targetScene);
        if (targetInventory != null)
        {
            return;
        }

        MoveRootToScene(sourceInventory.transform.root.gameObject, targetScene);

        GameObject sourceGlobalDragLayer = FindRootByName(sourceScene, "GlobalDragLayer");
        if (sourceGlobalDragLayer != null && FindRootByName(targetScene, "GlobalDragLayer") == null)
        {
            MoveRootToScene(sourceGlobalDragLayer, targetScene);
        }
    }

    private static void ImportPromptSupport(Scene sourceScene, Scene targetScene)
    {
        GameObject sourceFloatingPrompt = FindRootByName(sourceScene, "FloatingPrompt");
        if (sourceFloatingPrompt != null && FindRootByName(targetScene, "FloatingPrompt") == null)
        {
            MoveRootToScene(sourceFloatingPrompt, targetScene);
        }
    }

    private static void ImportEventSystem(Scene sourceScene, Scene targetScene)
    {
        EventSystem targetEventSystem = FindComponentInScene<EventSystem>(targetScene);
        if (targetEventSystem != null)
        {
            return;
        }

        EventSystem sourceEventSystem = FindComponentInScene<EventSystem>(sourceScene);
        if (sourceEventSystem != null)
        {
            MoveRootToScene(sourceEventSystem.transform.root.gameObject, targetScene);
        }
    }

    private static void ImportOrConfigureCamera(Scene sourceScene, Scene targetScene)
    {
        Camera sourceCamera = FindCameraWithFollow(sourceScene);
        if (sourceCamera == null)
        {
            return;
        }

        Camera targetCamera = FindCameraInScene(targetScene);
        CameraFollowController sourceFollow = sourceCamera.GetComponent<CameraFollowController>();
        GameObject targetPlayer = FindPlayerInScene(targetScene);

        if (targetCamera == null)
        {
            MoveRootToScene(sourceCamera.transform.root.gameObject, targetScene);
            targetCamera = FindCameraWithFollow(targetScene);
        }
        else if (sourceFollow != null)
        {
            CameraFollowController targetFollow = targetCamera.GetComponent<CameraFollowController>();
            if (targetFollow == null)
            {
                targetFollow = Undo.AddComponent<CameraFollowController>(targetCamera.gameObject);
            }

            targetFollow.Offset = sourceFollow.Offset;
            targetFollow.SmoothSpeed = sourceFollow.SmoothSpeed;
            targetFollow.TargetTransform = targetPlayer != null ? targetPlayer.transform : null;

            targetCamera.transform.position = sourceCamera.transform.position;
            targetCamera.transform.rotation = sourceCamera.transform.rotation;
            targetCamera.orthographic = sourceCamera.orthographic;
            targetCamera.orthographicSize = sourceCamera.orthographicSize;
            targetCamera.fieldOfView = sourceCamera.fieldOfView;
            targetCamera.nearClipPlane = sourceCamera.nearClipPlane;
            targetCamera.farClipPlane = sourceCamera.farClipPlane;
            targetCamera.clearFlags = sourceCamera.clearFlags;
            targetCamera.backgroundColor = sourceCamera.backgroundColor;
        }

        if (targetCamera != null && targetPlayer != null)
        {
            CameraFollowController follow = targetCamera.GetComponent<CameraFollowController>();
            if (follow != null)
            {
                follow.TargetTransform = targetPlayer.transform;
            }
        }
    }

    private static void EnsurePlayerInteractionConfigured(Scene sourceScene, Scene targetScene)
    {
        GameObject targetPlayer = FindPlayerInScene(targetScene);
        if (targetPlayer == null)
        {
            return;
        }

        PlayerInteraction targetInteraction = targetPlayer.GetComponent<PlayerInteraction>();
        PlayerInteraction sourceInteraction = FindComponentInScene<PlayerInteraction>(sourceScene);
        if (targetInteraction == null)
        {
            targetInteraction = Undo.AddComponent<PlayerInteraction>(targetPlayer);
        }

        if (sourceInteraction != null)
        {
            targetInteraction.InteractionRadius = sourceInteraction.InteractionRadius;
            targetInteraction.InteractableLayer = sourceInteraction.InteractableLayer;
            targetInteraction.HeightOffset = sourceInteraction.HeightOffset;
        }

        GameObject floatingPrompt = FindRootByName(targetScene, "FloatingPrompt");
        if (floatingPrompt != null)
        {
            targetInteraction.FloatingPromptUI = floatingPrompt.GetComponent<RectTransform>();
            targetInteraction.PromptText = floatingPrompt.GetComponentInChildren<Text>(true);
        }
    }

    private static void EnsureRuntimeBinderConfigured(Scene targetScene)
    {
        SceneRuntimePlayerBinder binder = FindComponentInScene<SceneRuntimePlayerBinder>(targetScene);
        if (binder == null)
        {
            GameObject binderObject = new GameObject("SceneRuntimePlayerBinder");
            Undo.RegisterCreatedObjectUndo(binderObject, "Create Scene Runtime Player Binder");
            EditorSceneManager.MoveGameObjectToScene(binderObject, targetScene);
            binder = binderObject.AddComponent<SceneRuntimePlayerBinder>();
        }

        GameObject floatingPrompt = FindRootByName(targetScene, "FloatingPrompt");
        if (floatingPrompt != null)
        {
            binder.FloatingPromptUI = floatingPrompt.GetComponent<RectTransform>();
            binder.FloatingPromptText = floatingPrompt.GetComponentInChildren<Text>(true);
        }

        Camera targetCamera = FindCameraWithFollow(targetScene);
        if (targetCamera != null)
        {
            binder.CameraFollow = targetCamera.GetComponent<CameraFollowController>();
        }
    }

    private static void MoveRootToScene(GameObject rootObject, Scene targetScene)
    {
        if (rootObject == null)
        {
            return;
        }

        Undo.SetTransformParent(rootObject.transform, null, "Move Root To Target Scene");
        EditorSceneManager.MoveGameObjectToScene(rootObject, targetScene);
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < objects.Length; i++)
        {
            T candidate = objects[i];
            if (candidate != null && candidate.gameObject.scene == scene)
            {
                return candidate;
            }
        }

        return null;
    }

    private static GameObject FindRootByName(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == objectName)
            {
                return roots[i];
            }
        }

        return null;
    }

    private static GameObject FindPlayerInScene(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            if (root.CompareTag("Player"))
            {
                return root;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < children.Length; j++)
            {
                if (children[j] != null && children[j].CompareTag("Player"))
                {
                    return children[j].gameObject;
                }
            }
        }

        return null;
    }

    private static Camera FindCameraInScene(Scene scene)
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null && candidate.gameObject.scene == scene)
            {
                return candidate;
            }
        }

        return null;
    }

    private static Camera FindCameraWithFollow(Scene scene)
    {
        CameraFollowController[] followControllers = Resources.FindObjectsOfTypeAll<CameraFollowController>();
        for (int i = 0; i < followControllers.Length; i++)
        {
            CameraFollowController follow = followControllers[i];
            if (follow != null && follow.gameObject.scene == scene)
            {
                return follow.GetComponent<Camera>();
            }
        }

        return FindCameraInScene(scene);
    }
}
