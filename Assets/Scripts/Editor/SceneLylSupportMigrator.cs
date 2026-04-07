using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

[InitializeOnLoad]
public static class SceneLylSupportMigrator
{
    private const string SourceScenePath = "Assets/Scenes/Scene_lyl.unity";
    private static bool _autoImportScheduled;

    static SceneLylSupportMigrator()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += TryAutoImportCurrentScene;
    }

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

        if (EnsureCoreGameplaySupport(targetScene))
        {
            EditorUtility.DisplayDialog(
                "Import Core Gameplay Support",
                "Camera follow, inventory support and prompt UI have been imported from Scene_lyl, and runtime player auto-binding has been enabled.",
                "OK");
        }
    }

    public static bool EnsureCoreGameplaySupport(Scene targetScene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
        {
            return false;
        }

        if (!targetScene.IsValid() || string.Equals(targetScene.path, SourceScenePath, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
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
            return true;
        }
        finally
        {
            EditorSceneManager.CloseScene(sourceScene, true);
        }
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
        {
            return;
        }

        if (mode == OpenSceneMode.Additive && string.Equals(scene.path, SourceScenePath, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ScheduleAutoImport(scene);
    }

    private static void TryAutoImportCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
        {
            return;
        }

        ScheduleAutoImport(SceneManager.GetActiveScene());
    }

    private static void ScheduleAutoImport(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
        {
            return;
        }

        if (!scene.IsValid() || string.Equals(scene.path, SourceScenePath, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_autoImportScheduled)
        {
            return;
        }

        _autoImportScheduled = true;
        EditorApplication.delayCall += () =>
        {
            _autoImportScheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
            {
                return;
            }

            if (!scene.IsValid())
            {
                return;
            }

            if (HasCoreGameplaySupport(scene))
            {
                return;
            }

            EnsureCoreGameplaySupport(scene);
        };
    }

    private static void ImportInventorySupport(Scene sourceScene, Scene targetScene)
    {
        InventoryScreenController sourceInventory = FindComponentInScene<InventoryScreenController>(sourceScene);
        if (sourceInventory == null)
        {
            return;
        }

        InventoryScreenController targetInventory = FindComponentInScene<InventoryScreenController>(targetScene);
        if (HasValidInventoryReferences(targetInventory))
        {
            return;
        }

        if (targetInventory != null)
        {
            GameObject existingRoot = targetInventory.transform.root.gameObject;
            if (existingRoot != null)
            {
                Undo.DestroyObjectImmediate(existingRoot);
            }
            targetInventory = null;
        }

        HashSet<GameObject> movedRoots = new HashSet<GameObject>();
        MoveReferencedRoot(sourceInventory.gameObject, targetScene, movedRoots);
        MoveReferencedRoot(sourceInventory.InventoryPanel != null ? sourceInventory.InventoryPanel.gameObject : null, targetScene, movedRoots);
        MoveReferencedRoot(sourceInventory.PocketGrid != null ? sourceInventory.PocketGrid.gameObject : null, targetScene, movedRoots);
        MoveReferencedRoot(sourceInventory.TacticalRigGrid != null ? sourceInventory.TacticalRigGrid.gameObject : null, targetScene, movedRoots);
        MoveReferencedRoot(sourceInventory.BackpackGrid != null ? sourceInventory.BackpackGrid.gameObject : null, targetScene, movedRoots);
        MoveReferencedRoot(sourceInventory.LootChestGrid != null ? sourceInventory.LootChestGrid.gameObject : null, targetScene, movedRoots);
        MoveReferencedRoot(sourceInventory.RigSlot != null ? sourceInventory.RigSlot.gameObject : null, targetScene, movedRoots);
        MoveReferencedRoot(sourceInventory.BackpackSlot != null ? sourceInventory.BackpackSlot.gameObject : null, targetScene, movedRoots);

        InventoryItemFactory sourceFactory = sourceInventory.GetComponent<InventoryItemFactory>();
        if (sourceFactory != null && sourceFactory.GlobalDragLayer != null)
        {
            MoveReferencedRoot(sourceFactory.GlobalDragLayer.gameObject, targetScene, movedRoots);
        }

        targetInventory = FindComponentInScene<InventoryScreenController>(targetScene);
        if (targetInventory == null && sourceInventory.gameObject.scene == targetScene)
        {
            targetInventory = sourceInventory;
        }

        if (targetInventory != null)
        {
            RebindInventoryReferences(targetInventory, targetScene);
            targetInventory.InitializeRuntimeScreen();
        }
    }

    private static void ImportPromptSupport(Scene sourceScene, Scene targetScene)
    {
        GameObject sourceFloatingPrompt = FindRootByName(sourceScene, "FloatingPrompt");
        if (sourceFloatingPrompt != null && FindRootByName(targetScene, "FloatingPrompt") == null)
        {
            MoveRootToScene(sourceFloatingPrompt, targetScene);
        }

        GameObject targetFloatingPrompt = FindRootByName(targetScene, "FloatingPrompt");
        if (targetFloatingPrompt != null)
        {
            targetFloatingPrompt.SetActive(false);
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
            floatingPrompt.SetActive(false);
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

    private static void MoveReferencedRoot(GameObject referencedObject, Scene targetScene, HashSet<GameObject> movedRoots)
    {
        if (referencedObject == null)
        {
            return;
        }

        GameObject rootObject = referencedObject.transform.root.gameObject;
        if (rootObject == null || rootObject.scene == targetScene)
        {
            return;
        }

        if (movedRoots.Contains(rootObject))
        {
            return;
        }

        movedRoots.Add(rootObject);
        MoveRootToScene(rootObject, targetScene);
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

    private static bool HasCoreGameplaySupport(Scene scene)
    {
        return HasValidInventoryReferences(FindComponentInScene<InventoryScreenController>(scene)) &&
               FindRootByName(scene, "FloatingPrompt") != null &&
               FindComponentInScene<EventSystem>(scene) != null &&
               FindCameraWithFollow(scene) != null &&
               FindComponentInScene<SceneRuntimePlayerBinder>(scene) != null;
    }

    private static bool HasValidInventoryReferences(InventoryScreenController controller)
    {
        return controller != null &&
               controller.InventoryPanel != null &&
               controller.PocketGrid != null &&
               controller.TacticalRigGrid != null &&
               controller.BackpackGrid != null &&
               controller.LootChestGrid != null &&
               controller.RigSlot != null &&
               controller.BackpackSlot != null;
    }

    private static void RebindInventoryReferences(InventoryScreenController controller, Scene targetScene)
    {
        if (controller == null)
        {
            return;
        }

        InventoryUIController[] grids = Resources.FindObjectsOfTypeAll<InventoryUIController>();
        EquipmentSlotUI[] slots = Resources.FindObjectsOfTypeAll<EquipmentSlotUI>();

        controller.InventoryPanel = FindSceneObjectByName(targetScene, "LeftPanel");
        controller.PocketGrid = FindSceneComponentByName<InventoryUIController>(targetScene, grids, "PocketGrid");
        controller.TacticalRigGrid = FindSceneComponentByName<InventoryUIController>(targetScene, grids, "RigInternalGrid");
        if (controller.TacticalRigGrid == null)
        {
            controller.TacticalRigGrid = FindSceneComponentByName<InventoryUIController>(targetScene, grids, "TacticalRigGrid");
        }

        controller.BackpackGrid = FindSceneComponentByName<InventoryUIController>(targetScene, grids, "BackpackGrid");
        controller.LootChestGrid = FindSceneComponentByName<InventoryUIController>(targetScene, grids, "LootChestPanel");
        if (controller.LootChestGrid == null)
        {
            controller.LootChestGrid = FindSceneComponentByName<InventoryUIController>(targetScene, grids, "LootChestGrid");
        }
        controller.RigSlot = FindSceneComponentByName<EquipmentSlotUI>(targetScene, slots, "RigSlot");
        controller.BackpackSlot = FindSceneComponentByName<EquipmentSlotUI>(targetScene, slots, "BackpackSlot");

        InventoryItemFactory inventoryItemFactory = controller.GetComponent<InventoryItemFactory>();
        if (inventoryItemFactory != null)
        {
            GameObject globalDragLayer = FindSceneObjectByName(targetScene, "GlobalDragLayer");
            if (globalDragLayer != null)
            {
                inventoryItemFactory.GlobalDragLayer = globalDragLayer.GetComponent<RectTransform>();
            }
        }
    }

    private static T FindSceneComponentByName<T>(Scene scene, T[] candidates, string objectName) where T : Component
    {
        if (candidates == null)
        {
            return null;
        }

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate != null && candidate.gameObject.scene == scene && candidate.gameObject.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static GameObject FindSceneObjectByName(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
            {
                continue;
            }

            if (root.name == objectName)
            {
                return root;
            }

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < children.Length; j++)
            {
                if (children[j] != null && children[j].gameObject.name == objectName)
                {
                    return children[j].gameObject;
                }
            }
        }

        return null;
    }
}
