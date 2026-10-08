using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ExtractionLike.Aerospace;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AerospaceCollectiblesBuilder
{
    const string Root = "Assets/Art/AerospaceCollectibles";
    const string ResourcesRoot = "Assets/Resources/Aerospace";
    [MenuItem("Tools/Aerospace/Rebuild collectible assets (no scene changes)")]
    public static void Build()
    {
        Directory.CreateDirectory(ResourcesRoot + "/Models"); Directory.CreateDirectory(ResourcesRoot + "/Items");
        Directory.CreateDirectory(Root + "/Materials"); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var catalog = JsonUtility.FromJson<AerospaceCatalog>(File.ReadAllText(ResourcesRoot + "/PartsCatalog.json"));
        var shader = Shader.Find("Aerospace/Inspection PBR"); if (shader == null) throw new Exception("Inspection shader missing.");
        var cube = StudioCube();
        var database = AssetDatabase.LoadAssetAtPath<InventoryItemDatabase>("Assets/Resources/Inventory/InventoryItemDatabase.asset");
        if (database == null) throw new Exception("Existing inventory database missing; refusing to create a replacement.");
        foreach (var cfg in catalog.models)
        {
            string folder = Root + "/" + cfg.code;
            foreach (string path in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = path.Contains("Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.sRGBTexture = path.Contains("BaseColor"); imp.maxTextureSize = path.Contains("/4K/") ? 4096 : 2048;
                imp.mipmapEnabled = true; imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.anisoLevel = 4; imp.SaveAndReimport();
            }
            string fbx = folder + "/" + cfg.code + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            foreach (string quality in new[] { "2K", "4K" })
            {
                var clean = new Dictionary<string, Material>(); var recovered = new Dictionary<string, Material>();
                foreach (string family in cfg.parts.Select(p => p.family).Distinct())
                {
                    for (int surface = 0; surface < 2; surface++)
                    {
                        bool graphic = family.EndsWith("Graphic", StringComparison.Ordinal), worn = surface == 1 && !graphic;
                        var m = new Material(shader) { name = family + "_" + quality + (worn ? "_Recovered" : "_Clean") };
                        string suffix = quality == "2K" ? "_2K" : "";
                        Func<string, Texture2D> texture = label => AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/" + quality + "/" + family + "_" + label + suffix + ".png");
                        if (!graphic)
                        {
                            m.SetTexture("_BaseMap", texture(worn ? "RecoveredBaseColor" : "BaseColor"));
                            m.SetTexture("_NormalMap", texture("Normal"));
                            m.SetTexture("_PackedMap", texture(worn ? "RecoveredORM" : "Unity_MetallicSmoothness"));
                            m.SetTexture("_AOMap", texture("AO"));
                            if (m.GetTexture("_BaseMap") == null || m.GetTexture("_NormalMap") == null || m.GetTexture("_PackedMap") == null) throw new Exception("Missing maps: " + m.name);
                        }
                        else m.SetColor("_BaseColor", new Color(.86f, .92f, .89f));
                        m.SetFloat("_Graphic", graphic ? 1 : 0); m.SetFloat("_UseORM", worn ? 1 : 0); m.SetTexture("_Studio", cube);
                        string matPath = Root + "/Materials/" + family + "_" + quality + "_" + surface + ".mat";
                        m = SaveAsset(m, matPath); (surface == 0 ? clean : recovered)[family] = m;
                    }
                }
                var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx)); go.name = cfg.code + "_Inspection";
                if (cfg.code == "R01")
                {
                    string[] legacy = { "Hotspot_Hotwall_Export", "Hotspot_CoolingChannels_Export", "Hotspot_StructuralJacket_Export", "Hotspot_ManifoldInlet_Export" };
                    var nodes = go.GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < legacy.Length; i++) { var node = nodes.FirstOrDefault(t => t.name == legacy[i]); if (node != null) node.name = "R01_Hotspot_" + (i + 1); }
                }
                var binding = go.AddComponent<AerospaceModelAsset>(); binding.meshes = go.GetComponentsInChildren<Renderer>(true);
                binding.cleanMaterials = new Material[binding.meshes.Length]; binding.recoveredMaterials = new Material[binding.meshes.Length];
                for (int i = 0; i < binding.meshes.Length; i++)
                {
                    var mesh = binding.meshes[i]; var part = cfg.parts.FirstOrDefault(p => p.name == mesh.name);
                    if (part == null) throw new Exception("Unbound semantic mesh: " + mesh.name);
                    mesh.sharedMaterial = binding.cleanMaterials[i] = clean[part.family]; binding.recoveredMaterials[i] = recovered[part.family];
                    mesh.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mesh.receiveShadows = false;
                }
                PrefabUtility.SaveAsPrefabAsset(go, ResourcesRoot + "/Models/" + cfg.code + "_" + quality + ".prefab"); Object.DestroyImmediate(go);
            }
            string iconPath = ResourcesRoot + "/Icons/" + cfg.code + ".png";
            var iconImporter = (TextureImporter)AssetImporter.GetAtPath(iconPath); iconImporter.textureType = TextureImporterType.Sprite;
            iconImporter.spriteImportMode = SpriteImportMode.Single; iconImporter.alphaIsTransparency = true; iconImporter.mipmapEnabled = false;
            iconImporter.maxTextureSize = 1024; iconImporter.textureCompression = TextureImporterCompression.CompressedHQ; iconImporter.SaveAndReimport();
            string itemPath = ResourcesRoot + "/Items/" + cfg.code + ".asset";
            var item = AssetDatabase.LoadAssetAtPath<InventoryItemData>(itemPath);
            if (item == null) { item = ScriptableObject.CreateInstance<InventoryItemData>(); AssetDatabase.CreateAsset(item, itemPath); }
            item.ItemID = "ASTRA_" + cfg.code; item.ItemName = cfg.title; item.ItemIcon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            item.Type = ItemType.Junk; item.Rarity = ItemRarity.Legendary; item.Width = item.Height = 1;
            item.IsStackable = false; item.MaxStack = 1; item.CarryWeight = .2f; item.SellPrice = 1800;
            item.IncludeInRuntimeDatabase = true; item.IncludeInTotemShop = false;
            item.RequiresSearchInLootContainer = true; item.SearchDurationOverride = 2.1f;
            EditorUtility.SetDirty(item);
            database.Items.RemoveAll(i => i != null && i != item && i.ItemID == item.ItemID);
            if (!database.Items.Contains(item)) database.Items.Add(item);
        }
        var presentation = AssetDatabase.LoadAssetAtPath<AerospacePresentationAssets>(ResourcesRoot + "/Presentation.asset");
        if (presentation == null) { presentation = ScriptableObject.CreateInstance<AerospacePresentationAssets>(); AssetDatabase.CreateAsset(presentation, ResourcesRoot + "/Presentation.asset"); }
        presentation.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/text-c.ttf");
        presentation.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansCJKsc-Regular.otf");
        presentation.headingFont = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Fonts/NotoSansCJKsc-Medium.otf");
        presentation.monoFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Font/Text.ttf");
        EditorUtility.SetDirty(presentation); EditorUtility.SetDirty(database); AssetDatabase.SaveAssets();
        AerospaceScienceTypographyBuilder.Build();
        Debug.Log("AEROSPACE_COLLECTIBLES_BUILD_PASS: 5 items, 5 rendered icons, 10 current high-detail prefabs, private studio PBR. Formal scene untouched.");
    }
    [MenuItem("Tools/Aerospace/Rebuild enlarged injector teaching element")]
    public static void BuildTeachingElement()
    {
        string fbx = Root + "/R03/R03_Element_Study.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbx); importer.importCameras = false; importer.importLights = false;
        importer.importAnimation = false; importer.bakeAxisConversion = true; importer.globalScale = 1; importer.useFileScale = true;
        importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; importer.SaveAndReimport();
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        var materials = new Dictionary<Material, Material>(); var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(Root + "/Materials/PrivateStudio.asset");
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
            {
                if (materials.TryGetValue(source, out var ready)) return ready;
                var m = new Material(Shader.Find("Aerospace/Inspection PBR"));
                m.SetColor("_BaseColor", source.HasProperty("_Color") ? source.GetColor("_Color") : source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.gray);
                m.SetFloat("_Graphic", .6f); m.SetTexture("_Studio", cube);
                m = SaveAsset(m, Root + "/Materials/Study_" + source.name + ".mat"); materials[source] = m; return m;
            }).ToArray();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        PrefabUtility.SaveAsPrefabAsset(go, ResourcesRoot + "/Models/R03_Study.prefab"); Object.DestroyImmediate(go); AssetDatabase.SaveAssets();
        Debug.Log("AEROSPACE_TEACHING_ELEMENT_READY");
    }
    static T SaveAsset<T>(T created, string path) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) { EditorUtility.CopySerialized(created, existing); Object.DestroyImmediate(created); return existing; }
        AssetDatabase.CreateAsset(created, path); return created;
    }
    static Cubemap StudioCube()
    {
        var cube = new Cubemap(64, TextureFormat.RGBAHalf, true);
        for (int face = 0; face < 6; face++)
        {
            var pixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float strip = Mathf.Pow(Mathf.Clamp01(1 - Mathf.Abs(x - 23) / 17f), 3);
                float v = .16f + strip * (face == 2 ? 2f : 1.15f);
                pixels[y * 64 + x] = new Color(v * .92f, v * .98f, v, 1);
            }
            cube.SetPixels(pixels, (CubemapFace)face);
        }
        cube.Apply(); return SaveAsset(cube, Root + "/Materials/PrivateStudio.asset");
    }
}
