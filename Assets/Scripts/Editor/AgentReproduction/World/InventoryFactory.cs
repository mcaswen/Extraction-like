using UnityEditor;
using UnityEngine;

namespace AgentReproduction.World
{
    public static class InventoryFactory
    {
        public static InventoryScreenController Create(TestWorldBuilder world)
        {
            var staging = world.Root("Inventory fixture", false);
            var canvas = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Canvas.prefab"), staging.transform);
            foreach (var raid in canvas.GetComponentsInChildren<RaidFlowController>(true)) Object.DestroyImmediate(raid);
            staging.SetActive(true);
            return canvas.GetComponentInChildren<InventoryScreenController>(true);
        }
    }
}
