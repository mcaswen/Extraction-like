using System;
using System.IO;
using System.Linq;
using ExtractionLike.Aerospace;
using UnityEditor;
using UnityEngine;

public static class AerospaceCollectiblesValidation
{
    public static void AssetsForBatch()
    {
        var catalog = AerospaceCatalog.Load(); Require(catalog.models.Length == 5, "Exactly five types required.");
        var database = Resources.Load<InventoryItemDatabase>("Inventory/InventoryItemDatabase");
        foreach (var p in catalog.models)
        {
            var item = Resources.Load<InventoryItemData>("Aerospace/Items/" + p.code);
            Require(item != null && item.ItemIcon != null && item.ItemIcon.texture.width >= 512, "Missing icon/item " + p.code);
            Require(database.Items.Count(i => i != null && i.ItemID == item.ItemID) == 1, "Database duplicate " + p.code);
            Require(item.Width == 1 && item.Height == 1 && !item.IsStackable && !item.IncludeInTotemShop, "Inventory rules " + p.code);
            foreach (string q in new[] { "2K", "4K" })
            {
                var prefab = Resources.Load<GameObject>("Aerospace/Models/" + p.code + "_" + q);
                Require(prefab != null, "Missing prefab " + p.code + q);
                var asset = prefab.GetComponent<AerospaceModelAsset>(); Require(asset != null && asset.meshes.Length == p.parts.Length, "Missing mesh binding " + p.code);
                Require(prefab.GetComponentsInChildren<Collider>(true).Length == 0, "Inspection must not contain physics.");
                var transforms = prefab.GetComponentsInChildren<Transform>(true);
                foreach (var h in p.hotspots) Require(transforms.Any(t => t.name == h.anchor), "Missing hotspot " + h.anchor);
                foreach (var material in asset.cleanMaterials.Concat(asset.recoveredMaterials))
                {
                    Require(material != null && material.shader.name == "Aerospace/Inspection PBR", "Wrong material");
                    if (material.GetFloat("_Graphic") < .5f) Require(material.GetTexture("_NormalMap") != null && material.GetTexture("_PackedMap") != null, "Missing PBR maps");
                }
                long triangles = prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => (long)f.sharedMesh.triangles.Length / 3);
                Debug.Log("AEROSPACE_ASSET_VERIFIED " + p.code + " " + q + " triangles=" + triangles + " meshes=" + asset.meshes.Length + " hotspots=4");
            }
        }
        var clip = Resources.Load<AudioClip>("Aerospace/RareDiscovery"); Require(clip != null && clip.length > 1, "Missing discovery audio");
        Require(Resources.Load<GameObject>("Aerospace/Models/R03_Study") != null, "Missing injector teaching element");
        Require(Resources.Load<AerospacePresentationAssets>("Aerospace/Presentation").bodyFont != null, "Missing readable Chinese font");
        var presentation = Resources.Load<AerospacePresentationAssets>("Aerospace/Presentation");
        Require(presentation.inspectionHeading != null && presentation.inspectionBody != null && presentation.inspectionMono != null, "Missing science UI SDF typography");
        Debug.Log("AEROSPACE_ASSET_VALIDATION_PASS");
    }
    public static void ConfigureIsolatedTests()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        if (!root.StartsWith("/tmp/astra-collectibles-test.", StringComparison.Ordinal) && !root.StartsWith("/private/tmp/astra-collectibles-test.", StringComparison.Ordinal)) throw new Exception("Only a disposable test clone may be configured.");
        PlayerSettings.companyName = "AnomalySearch.Automation"; PlayerSettings.productName = "AgentRepro_Aerospace_20261003";
        AssetDatabase.SaveAssets();
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
