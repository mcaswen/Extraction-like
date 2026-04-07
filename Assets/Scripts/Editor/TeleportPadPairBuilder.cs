using UnityEditor;
using UnityEngine;

public static class TeleportPadPairBuilder
{
    [MenuItem("Tools/Whitebox/Create Teleport Pair For Selected Ladder")]
    private static void CreateTeleportPairForSelectedLadder()
    {
        if (Selection.activeTransform == null)
        {
            EditorUtility.DisplayDialog(
                "Create Teleport Pair",
                "Please select the ladder object first.",
                "OK");
            return;
        }

        Transform ladder = Selection.activeTransform;
        Bounds ladderBounds = CalculateBounds(ladder.gameObject);
        Vector3 center = ladderBounds.center;

        Vector3 bottomPosition = new Vector3(center.x, ladderBounds.min.y + 0.12f, center.z - 0.9f);
        Vector3 topPosition = new Vector3(center.x, ladderBounds.max.y + 0.12f, center.z - 0.9f);

        Transform pairRoot = new GameObject("TeleportPair").transform;
        Undo.RegisterCreatedObjectUndo(pairRoot.gameObject, "Create Teleport Pair");
        pairRoot.SetParent(ladder.parent, true);

        TeleportPadController bottomPad = CreatePad(pairRoot, "TeleportPad_Bottom", bottomPosition);
        TeleportPadController topPad = CreatePad(pairRoot, "TeleportPad_Top", topPosition);

        bottomPad.DestinationPoint = topPad.transform;
        topPad.DestinationPoint = bottomPad.transform;

        Selection.activeGameObject = pairRoot.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private static TeleportPadController CreatePad(Transform parent, string name, Vector3 worldPosition)
    {
        GameObject padObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Undo.RegisterCreatedObjectUndo(padObject, $"Create {name}");
        padObject.name = name;
        padObject.transform.SetParent(parent, true);
        padObject.transform.position = worldPosition;
        padObject.transform.localScale = new Vector3(0.9f, 0.08f, 0.9f);

        Renderer rendererComponent = padObject.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.92f, 0.98f, 1f, 1f);
            rendererComponent.sharedMaterial = material;
        }

        CapsuleCollider collider = padObject.GetComponent<CapsuleCollider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        BoxCollider trigger = padObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.6f, 1.1f, 1.6f);
        trigger.center = new Vector3(0f, 0.35f, 0f);

        TeleportPadController padController = padObject.AddComponent<TeleportPadController>();
        padController.PadName = name;
        return padController;
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }

            return bounds;
        }

        return new Bounds(target.transform.position, Vector3.one);
    }
}
