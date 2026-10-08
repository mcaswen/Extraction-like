using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ExtractionLike.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Object = UnityEngine.Object;

/// <summary>Scene-local art direction. Never bakes lighting/navigation or edits source foliage assets.</summary>
public static partial class AerospaceSceneLayoutBuilder
{
    public const string LightingPaletteRoot = "Aerospace_LightingAndVegetation_V1";
    public const string LightingPaletteAssets = "Assets/Art/Environment/AerospaceLightingAndVegetation_V1";
    private const string LightingPaletteBackup = "UserSettings/SceneBackups/Scenezl_Final 1_Before_LightingAndVegetation_V1.unity";
    private const string LightingPalettePreviews = "UserSettings/ScenePreviews/Aerospace_LightingAndVegetation_V1";

    [MenuItem("Tools/Extraction-like/Lighting And Vegetation/Install Aerospace Palette V1")]
    public static void BuildLightingAndVegetationV1()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play Mode。");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureSceneBackup(LightingPaletteBackup);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (FindByName(scene, LightingPaletteRoot) != null)
            throw new InvalidOperationException("Lighting palette already exists. Artist settings will not be rebuilt/overwritten.");
        var cycle = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AerospaceDayNightCycle>(true)).Single();
        var navigation = CaptureInvestigationNavigation(scene);
        string originalComponents = CaptureLightingPaletteProtectedState(scene);
        var originalFiles = new Dictionary<string, string>();
        foreach (string section in new[] { "Materials", "Terrain", "TerrainLayers", "TreePrototypes", "Profiles", "Shaders" })
            EnsureAssetFolder(LightingPaletteAssets + "/" + section);
        Shader foliageShader = EnsureAerospaceFoliageShader();
        var materialCopies = new Dictionary<Material, Material>();
        var prototypeCopies = new Dictionary<GameObject, GameObject>();
        var layerCopies = new Dictionary<TerrainLayer, TerrainLayer>();
        var dataCopies = new Dictionary<TerrainData, TerrainData>();
        var rendererBindings = new List<AerospaceLightingVegetationProfile.RendererBinding>();
        var terrainBindings = new List<AerospaceLightingVegetationProfile.TerrainBinding>();
        foreach (Renderer renderer in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>(true)))
        {
            Material[] original = renderer.sharedMaterials;
            Material[] styled = original.Select(m => PaletteMaterialCopy(m, materialCopies, foliageShader, originalFiles)).ToArray();
            if (!AerospaceLightingVegetationProfile.MaterialsEqual(original, styled))
                rendererBindings.Add(new AerospaceLightingVegetationProfile.RendererBinding { renderer = renderer, original = original, styled = styled });
        }
        foreach (Terrain terrain in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)))
        {
            TerrainData original = terrain.terrainData;
            RememberOriginalPaletteFile(original, originalFiles);
            if (!dataCopies.TryGetValue(original, out TerrainData styled))
            {
                string path = LightingPaletteAssets + "/Terrain/TD_" + AssetKey(original) + ".asset";
                if (File.Exists(path)) throw new IOException("Refusing to overwrite an existing terrain art copy: " + path);
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original), path)) throw new IOException("Could not copy original terrain.");
                styled = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                styled.name = "Aerospace_" + original.name;
                styled.terrainLayers = original.terrainLayers.Select(layer => PaletteTerrainLayer(layer, layerCopies, originalFiles)).ToArray();
                var trees = original.treePrototypes.Select(tree => new TreePrototype
                {
                    bendFactor = tree.bendFactor,
                    prefab = PalettePrototype(tree.prefab, prototypeCopies, materialCopies, foliageShader, originalFiles)
                }).ToArray();
                styled.treePrototypes = trees;
                var details = original.detailPrototypes;
                foreach (DetailPrototype detail in details)
                {
                    detail.prototype = PalettePrototype(detail.prototype, prototypeCopies, materialCopies, foliageShader, originalFiles);
                    if (detail.renderMode == DetailRenderMode.Grass || detail.renderMode == DetailRenderMode.GrassBillboard)
                    {
                        detail.healthyColor = new Color(.38f, .51f, .32f);
                        detail.dryColor = new Color(.54f, .51f, .34f);
                    }
                }
                styled.detailPrototypes = details;
                styled.RefreshPrototypes();
                styled.SetBaseMapDirty();
                EditorUtility.SetDirty(styled);
                AssertPaletteTerrainCopy(original, styled);
                dataCopies.Add(original, styled);
            }
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null && collider.terrainData != original) throw new InvalidOperationException("Original terrain/collider binding mismatch.");
            terrainBindings.Add(new AerospaceLightingVegetationProfile.TerrainBinding { terrain = terrain, collider = collider, original = original, styled = styled });
        }
        var cameraBindings = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true))
            .Where(camera => camera.CompareTag("MainCamera"))
            .Select(camera => camera.GetComponent<UniversalAdditionalCameraData>()).Where(data => data != null)
            .Select(data => new AerospaceLightingVegetationProfile.CameraBinding { camera = data, originalPostProcessing = data.renderPostProcessing }).ToArray();
        if (cameraBindings.Length != 1) throw new InvalidOperationException("Expected exactly one existing gameplay camera with URP camera data.");
        GameObject root = new GameObject(LightingPaletteRoot);
        var modifier = root.AddComponent<NavMeshModifier>(); modifier.ignoreFromBuild = true; modifier.applyToChildren = true;
        SetSurveyVisualOnly(root);
        Volume volume = root.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20;
        // Main camera already includes Default layer. The controller stays non-interactive on Ignore Raycast;
        // put the renderer-free Volume on its own Default-layer child so the existing volume mask is preserved.
        Object.DestroyImmediate(volume);
        var volumeObject = new GameObject("World_Grading_No_UI_Or_Gameplay"); volumeObject.transform.SetParent(root.transform, false);
        volume = volumeObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20;
        volume.sharedProfile = CreatePaletteVolume();
        var localLights = new List<AerospaceLightingVegetationProfile.LocalLightBinding>();
        AddPaletteBaseLight(root.transform, "LF01_Warm_Base_Fill", new Vector3(180, 9, -15), localLights);
        AddPaletteBaseLight(root.transform, "LF02_Warm_Base_Fill", new Vector3(218, 9, -155), localLights);
        Light wreckFill = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Light>(true))
            .Single(light => light.name == "Wreck_Cool_Fill" && light.gameObject.activeInHierarchy);
        localLights.Add(new AerospaceLightingVegetationProfile.LocalLightBinding
        {
            light = wreckFill, originalColor = wreckFill.color, originalIntensity = wreckFill.intensity,
            surveyColor = new Color(.92f, .87f, .72f), dayIntensity = 25, nightIntensity = 55
        });
        var profile = root.AddComponent<AerospaceLightingVegetationProfile>();
        profile.Configure(cycle, volume, rendererBindings.ToArray(), terrainBindings.ToArray(), cameraBindings, localLights.ToArray());
        cycle.artDirection = profile;
        cycle.ApplyPhase(cycle.startPhase);
        AssertInvestigationSnapshotEqual(navigation, CaptureInvestigationNavigation(scene));
        if (originalComponents != CaptureLightingPaletteProtectedState(scene))
            throw new InvalidOperationException("Original transforms, physics, targets or gameplay settings changed; formal scene not saved.");
        foreach (var file in originalFiles)
            if (file.Value != PaletteFileHash(file.Key)) throw new InvalidOperationException("Source asset changed: " + file.Key);
        AssertNoInvestigationNavigationSources(scene, root.transform);
        VerifyLightingAndVegetationV1(scene);
        EditorUtility.SetDirty(cycle); EditorUtility.SetDirty(profile);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save formal scene.");
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(LightingPalettePreviews);
        File.WriteAllLines(LightingPalettePreviews + "/source-asset-hashes.txt", originalFiles.OrderBy(pair => pair.Key).Select(pair => pair.Value + "  " + pair.Key));
        File.WriteAllText(LightingPalettePreviews + "/navigation-verification.txt", "Lighting / vegetation V1\nAgents=" + navigation.Agents + "\nProbes=" + navigation.Probes + "\nCompleteBaseline=" + navigation.Complete + "\nVertices=" + navigation.Vertices + "\nGeometry=" + navigation.GeometryHash + "\nOriginal geometry, transforms, physics and gameplay configuration identical.\nAll source materials, prefabs, terrain layers and terrain data unchanged.\n", Encoding.UTF8);
        Debug.Log("[Lighting Palette] PASS saved. Scene renderer bindings=" + rendererBindings.Count + ", terrain copies=" + dataCopies.Count + ", foliage materials=" + materialCopies.Count + ", prototype copies=" + prototypeCopies.Count);
        if (Application.isBatchMode) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static void AddPaletteBaseLight(Transform parent, string name, Vector3 position, List<AerospaceLightingVegetationProfile.LocalLightBinding> bindings)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position;
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        Light light = go.AddComponent<Light>(); light.type = LightType.Point; light.range = 19; light.shadows = LightShadows.None;
        light.color = new Color(1, .84f, .66f); light.intensity = 18;
        bindings.Add(new AerospaceLightingVegetationProfile.LocalLightBinding
        { light = light, originalColor = light.color, originalIntensity = light.intensity, surveyColor = light.color, dayIntensity = 18, nightIntensity = 35 });
    }

    private static VolumeProfile CreatePaletteVolume()
    {
        string path = LightingPaletteAssets + "/Profiles/VP_Aerospace_Readable_DayNight.asset";
        if (File.Exists(path)) throw new IOException("Refusing to overwrite " + path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "VP_Aerospace_Readable_DayNight";
        AssetDatabase.CreateAsset(profile, path);
        var color = profile.Add<ColorAdjustments>(true); color.postExposure.Override(.05f); color.contrast.Override(4); color.saturation.Override(-6);
        var bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.3f); bloom.intensity.Override(.10f); bloom.scatter.Override(.45f);
        bloom.highQualityFiltering.Override(false);
        var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.Neutral);
        foreach (VolumeComponent component in profile.components) { component.name = component.GetType().Name; AssetDatabase.AddObjectToAsset(component, profile); }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static Shader EnsureAerospaceFoliageShader()
    {
        string path = LightingPaletteAssets + "/Shaders/AerospaceFoliageLit.shader";
        if (!File.Exists(path))
        {
            string source = File.ReadAllText("Library/PackageCache/com.unity.render-pipelines.universal@14.0.12/Shaders/Lit.shader");
            source = source.Replace("Shader \"Universal Render Pipeline/Lit\"", "Shader \"ExtractionLike/Environment/Aerospace Foliage Lit\"")
                .Replace("Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl", "Assets/Shader/ToonOutlines/AerospaceFoliageLitInput.hlsl");
            File.WriteAllText(path, "// Derived from Unity URP 14.0.12 Lit.shader under the Unity Companion License.\n// All URP passes preserved; only the surface-albedo initialization is wrapped.\n" + source);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Foliage palette shader could not compile.");
        return shader;
    }

    private static bool IsPaletteVegetation(Material material)
    {
        if (material == null) return false;
        string path = AssetDatabase.GetAssetPath(material);
        return material.shader.name == "SyntyStudios/VegitationShader" ||
            path.StartsWith("Assets/Art/Models/Visual Effect/Fbx_大树", StringComparison.Ordinal) ||
            path.StartsWith("Assets/Art/Models/Visual Effect/Fbx_中树", StringComparison.Ordinal) ||
            path.StartsWith("Assets/Art/Models/Visual Effect/Fbx_小树", StringComparison.Ordinal) ||
            path.StartsWith("Assets/Art/Models/Island/SenceUpdate/Tree", StringComparison.Ordinal);
    }

    private static Material PaletteMaterialCopy(Material original, Dictionary<Material, Material> copies, Shader shader, Dictionary<string, string> hashes)
    {
        if (!IsPaletteVegetation(original)) return original;
        if (copies.TryGetValue(original, out Material existing)) return existing;
        RememberOriginalPaletteFile(original, hashes);
        string path = LightingPaletteAssets + "/Materials/M_Foliage_" + AssetKey(original) + ".mat";
        if (File.Exists(path)) throw new IOException("Refusing to overwrite " + path);
        var styled = new Material(original) { name = "M_Foliage_" + original.name, enableInstancing = true };
        if (original.shader.name == "Universal Render Pipeline/Lit")
        {
            styled.shader = shader;
            styled.SetColor("_BaseColor", original.GetColor("_BaseColor") * new Color(.94f, .97f, .96f, 1));
            styled.SetFloat("_Smoothness", .12f); styled.SetFloat("_Metallic", 0);
            styled.SetFloat("_SpecularHighlights", 0); styled.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
        else
        {
            SetPaletteColor(styled, "_LeafBaseColour", new Color(.30f, .43f, .23f));
            SetPaletteColor(styled, "_LeafNoiseColour", new Color(.24f, .35f, .20f));
            SetPaletteColor(styled, "_LeafNoiseLargeColour", new Color(.39f, .49f, .29f));
            SetPaletteFloat(styled, "_LeafSmoothness", .12f); SetPaletteFloat(styled, "_LeafMetallic", 0);
            SetPaletteFloat(styled, "_TrunkSmoothness", .10f); SetPaletteFloat(styled, "_EmissiveAmount", 0);
        }
        AssetDatabase.CreateAsset(styled, path); copies.Add(original, styled);
        return styled;
    }

    private static void SetPaletteColor(Material material, string property, Color color)
    {
        if (!material.HasProperty(property)) return;
        color.a = material.GetColor(property).a; material.SetColor(property, color);
    }
    private static void SetPaletteFloat(Material material, string property, float value) { if (material.HasProperty(property)) material.SetFloat(property, value); }

    private static GameObject PalettePrototype(GameObject original, Dictionary<GameObject, GameObject> copies,
        Dictionary<Material, Material> materials, Shader shader, Dictionary<string, string> hashes)
    {
        if (original == null) return null;
        if (copies.TryGetValue(original, out GameObject copy)) return copy;
        RememberOriginalPaletteFile(original, hashes);
        if (!original.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(IsPaletteVegetation)) return original;
        string path = LightingPaletteAssets + "/TreePrototypes/PFB_Foliage_" + AssetKey(original) + ".prefab";
        if (File.Exists(path)) throw new IOException("Refusing to overwrite " + path);
        GameObject instance = Object.Instantiate(original); instance.name = "PFB_Foliage_" + original.name;
        try
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => PaletteMaterialCopy(m, materials, shader, hashes)).ToArray();
            copy = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool saved);
            if (!saved || copy == null) throw new IOException("Could not save foliage visual copy.");
            if (PalettePrototypeGeometry(original) != PalettePrototypeGeometry(copy)) throw new InvalidOperationException("Foliage geometry or colliders changed.");
            copies.Add(original, copy);
            return copy;
        }
        finally { Object.DestroyImmediate(instance); }
    }

    private static TerrainLayer PaletteTerrainLayer(TerrainLayer original, Dictionary<TerrainLayer, TerrainLayer> copies, Dictionary<string, string> hashes)
    {
        if (original == null) return null;
        if (copies.TryGetValue(original, out TerrainLayer copy)) return copy;
        RememberOriginalPaletteFile(original, hashes);
        bool vegetation = original.name.Contains("Grass") || original.name.Contains("Moss") || original.name.Contains("Leaves");
        if (!vegetation) return original;
        copy = Object.Instantiate(original); copy.name = "TL_Aerospace_" + original.name;
        Vector4 remap = original.diffuseRemapMax;
        copy.diffuseRemapMax = new Vector4(remap.x * .68f, remap.y * .86f, remap.z * .83f, remap.w);
        copy.smoothness = Mathf.Min(original.smoothness, .12f);
        AssetDatabase.CreateAsset(copy, LightingPaletteAssets + "/TerrainLayers/TL_" + AssetKey(original) + ".terrainlayer");
        copies.Add(original, copy); return copy;
    }

    private static string AssetKey(Object value)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id)) throw new InvalidOperationException("Asset needs a persistent identifier.");
        return guid.Substring(0, 12) + "_" + id;
    }
    private static void RememberOriginalPaletteFile(Object value, Dictionary<string, string> hashes)
    {
        string path = AssetDatabase.GetAssetPath(value);
        if (File.Exists(path) && !hashes.ContainsKey(path)) hashes.Add(path, PaletteFileHash(path));
    }
    private static string PaletteFileHash(string path)
    {
        using (var input = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(input));
    }

    public static string PalettePrototypeGeometry(GameObject prefab)
    {
        var log = new StringBuilder();
        foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
        {
            string relative = transform == prefab.transform ? "." : AnimationUtility.CalculateTransformPath(transform, prefab.transform);
            log.AppendLine(relative + "|" + transform.localPosition.ToString("F7") + "|" + transform.localRotation.ToString("F7") + "|" + transform.localScale.ToString("F7") + "|" + transform.gameObject.layer + "|" + transform.gameObject.activeSelf);
            foreach (MeshFilter filter in transform.GetComponents<MeshFilter>()) log.AppendLine("Mesh=" + AssetDatabase.GetAssetPath(filter.sharedMesh) + "/" + (filter.sharedMesh == null ? "null" : AssetKey(filter.sharedMesh)));
            foreach (Collider collider in transform.GetComponents<Collider>())
            {
                string json = EditorJsonUtility.ToJson(collider);
                json = Regex.Replace(json, "\"m_GameObject\":\\{.*?\\}", "\"m_GameObject\":{}");
                json = Regex.Replace(json, "\"m_CorrespondingSourceObject\":\\{.*?\\}", "\"m_CorrespondingSourceObject\":{}");
                json = Regex.Replace(json, "\"m_PrefabInstance\":\\{.*?\\}", "\"m_PrefabInstance\":{}");
                json = Regex.Replace(json, "\"m_PrefabAsset\":\\{.*?\\}", "\"m_PrefabAsset\":{}");
                log.AppendLine(collider.GetType().FullName + "=" + json);
            }
        }
        return log.ToString();
    }

    public static string PaletteTerrainGeometry(TerrainData data)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(data.size.x); writer.Write(data.size.y); writer.Write(data.size.z);
                writer.Write(data.heightmapResolution); writer.Write(data.alphamapResolution); writer.Write(data.detailResolution);
                foreach (float value in data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution)) writer.Write(value);
                foreach (bool value in data.GetHoles(0, 0, data.holesResolution, data.holesResolution)) writer.Write(value);
                foreach (float value in data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight)) writer.Write(value);
                writer.Write(data.treeInstanceCount);
                foreach (TreeInstance tree in data.treeInstances)
                {
                    writer.Write(tree.position.x); writer.Write(tree.position.y); writer.Write(tree.position.z);
                    writer.Write(tree.prototypeIndex); writer.Write(tree.rotation); writer.Write(tree.widthScale); writer.Write(tree.heightScale);
                    writer.Write(tree.color.r); writer.Write(tree.color.g); writer.Write(tree.color.b); writer.Write(tree.color.a);
                    writer.Write(tree.lightmapColor.r); writer.Write(tree.lightmapColor.g); writer.Write(tree.lightmapColor.b); writer.Write(tree.lightmapColor.a);
                }
                foreach (TreePrototype tree in data.treePrototypes)
                { writer.Write(tree.bendFactor); writer.Write(tree.prefab == null ? "null" : PalettePrototypeGeometry(tree.prefab)); }
                writer.Write(data.detailPrototypes.Length);
                for (int index = 0; index < data.detailPrototypes.Length; index++)
                {
                    DetailPrototype detail = data.detailPrototypes[index];
                    writer.Write(detail.minWidth); writer.Write(detail.maxWidth); writer.Write(detail.minHeight); writer.Write(detail.maxHeight);
                    writer.Write((int)detail.renderMode); writer.Write(detail.usePrototypeMesh);
                    writer.Write(detail.noiseSeed); writer.Write(detail.noiseSpread); writer.Write(detail.holeEdgePadding); writer.Write(detail.useInstancing);
                    writer.Write(AssetDatabase.GetAssetPath(detail.prototypeTexture));
                    writer.Write(detail.prototype == null ? "null" : PalettePrototypeGeometry(detail.prototype));
                    foreach (int value in data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, index)) writer.Write(value);
                }
            }
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream.ToArray()));
        }
    }
    public static void AssertPaletteTerrainCopy(TerrainData original, TerrainData styled)
    {
        if (PaletteTerrainGeometry(original) != PaletteTerrainGeometry(styled)) throw new InvalidOperationException("Terrain visual copy changed terrain/trees/detail geometry: " + original.name);
    }

    public static string CaptureLightingPaletteProtectedState(Scene scene)
    {
        var entries = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == LightingPaletteRoot) continue;
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                entries.Add(HierarchyPath(transform) + "|" + transform.localPosition.ToString("F7") + "|" + transform.localRotation.ToString("F7") + "|" + transform.localScale.ToString("F7") + "|" + transform.gameObject.activeSelf + "|" + transform.gameObject.layer);
                foreach (Component component in transform.GetComponents<Component>())
                {
                    if (component == null) { entries.Add(HierarchyPath(transform) + "|missing-existing-script"); continue; }
                    string json = EditorJsonUtility.ToJson(component);
                    if (component is Light) continue; // Only explicitly bound light color/intensity/shadows may change.
                    if (component is Renderer) json = Regex.Replace(json, "\"m_Materials\":\\[.*?\\]", "\"m_Materials\":[]");
                    if (component is Terrain || component is TerrainCollider) json = Regex.Replace(json, "\"m_TerrainData\":\\{.*?\\}", "\"m_TerrainData\":{}");
                    if (component is UniversalAdditionalCameraData) json = Regex.Replace(json, "\"m_RenderPostProcessing\":(true|false)", "\"m_RenderPostProcessing\":false");
                    if (component is AerospaceDayNightCycle) json = Regex.Replace(json, "\"artDirection\":\\{.*?\\}", "\"artDirection\":{}");
                    entries.Add(HierarchyPath(transform) + "|" + component.GetType().FullName + "|" + json);
                }
            }
        }
        entries.Sort(StringComparer.Ordinal); return string.Join("\n", entries);
    }

    public static void VerifyLightingAndVegetationV1(Scene scene)
    {
        Transform root = FindByName(scene, LightingPaletteRoot);
        if (root == null) throw new InvalidOperationException("Missing lighting palette root.");
        var profile = root.GetComponent<AerospaceLightingVegetationProfile>();
        if (profile == null || !profile.IsConfigured || profile.cycle == null || profile.cycle.artDirection != profile) throw new InvalidOperationException("Unconfigured palette / cycle hook.");
        if (root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Renderer>(true).Length != 0 || root.GetComponentsInChildren<NavMeshAgent>(true).Length != 0 || root.GetComponentsInChildren<NavMeshObstacle>(true).Length != 0)
            throw new InvalidOperationException("Lighting root must not create geometry, physics or navigation.");
        var modifier = root.GetComponent<NavMeshModifier>();
        if (modifier == null || !modifier.ignoreFromBuild || !modifier.AffectsAgentType(0) || !modifier.AffectsAgentType(1234)) throw new InvalidOperationException("Lighting root must ignore all navigation builds.");
        foreach (var binding in profile.Terrains)
        {
            AssertPaletteTerrainCopy(binding.original, binding.styled);
            if (binding.terrain.terrainData != (root.gameObject.activeInHierarchy ? binding.styled : binding.original)) throw new InvalidOperationException("Unexpected terrain comparison binding.");
        }
        foreach (var binding in profile.Renderers)
            foreach (Material material in binding.styled)
                if (material == null || material.shader == null || ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("Foliage material/shader invalid.");
    }

    [MenuItem("Tools/Extraction-like/Lighting And Vegetation/Render V1 Comparison")]
    public static void RenderLightingAndVegetationV1()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var profile = FindByName(scene, LightingPaletteRoot).GetComponent<AerospaceLightingVegetationProfile>();
        VerifyLightingAndVegetationV1(scene);
        Directory.CreateDirectory(LightingPalettePreviews);
        bool active = profile.gameObject.activeSelf;
        var cycle = profile.cycle;
        float phase = cycle.CurrentPhase;
        var sky = new Material(cycle.skyboxVersion2) { hideFlags = HideFlags.HideAndDontSave };
        var originalSky = RenderSettings.skybox;
        try
        {
            foreach (bool styled in new[] { false, true })
            {
                profile.gameObject.SetActive(styled);
                foreach (var time in new[] { new { name = "day", phase = 0f }, new { name = "sunset", phase = .25f }, new { name = "night", phase = .5f } })
                {
                    cycle.ApplyPhase(time.phase, sky);
                    string prefix = LightingPalettePreviews + "/" + (styled ? "after" : "before") + "-" + time.name;
                    RenderPaletteView(prefix + "-overview.png", new Vector3(332, 105, -52), new Vector3(247, 10, 65), 54);
                    RenderPaletteView(prefix + "-base.png", new Vector3(212, 46, 43), new Vector3(173, 5, -43), 50);
                    if (time.name != "sunset")
                    {
                        RenderPaletteView(prefix + "-dragon.png", new Vector3(202.8f, 116, 107), new Vector3(307.8f, 29, 232), 48);
                        RenderPaletteView(prefix + "-wreck.png", new Vector3(332, 39, -19), new Vector3(291, 9, 18), 48);
                    }
                }
            }
        }
        finally
        {
            profile.gameObject.SetActive(active); cycle.ApplyPhase(phase, sky);
            RenderSettings.skybox = originalSky; Object.DestroyImmediate(sky);
        }
        Debug.Log("[Lighting Palette] Day/sunset/night before-after previews rendered without saving temporary lighting: " + Path.GetFullPath(LightingPalettePreviews));
        if (Application.isBatchMode) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [MenuItem("Tools/Extraction-like/Lighting And Vegetation/Use V1 - Aerospace Palette")]
    public static void UseLightingPaletteV1() => SetLightingPaletteComparison(true);

    [MenuItem("Tools/Extraction-like/Lighting And Vegetation/Use Original - Before V1")]
    public static void UseLightingPaletteOriginal() => SetLightingPaletteComparison(false);

    private static void SetLightingPaletteComparison(bool active)
    {
        RequireEditMode();
        Scene scene = SceneManager.GetActiveScene();
        Transform root = FindByName(scene, LightingPaletteRoot);
        if (root == null) throw new InvalidOperationException("请先打开正式场景 Scenezl_Final 1。");
        var profile = root.GetComponent<AerospaceLightingVegetationProfile>();
        Undo.RecordObject(root.gameObject, "Compare aerospace lighting palette");
        foreach (var binding in profile.Renderers) if (binding.renderer != null) Undo.RecordObject(binding.renderer, "Compare foliage materials");
        foreach (var binding in profile.Terrains)
        {
            Undo.RecordObject(binding.terrain, "Compare terrain art");
            if (binding.collider != null) Undo.RecordObject(binding.collider, "Compare terrain art");
        }
        foreach (var binding in profile.Cameras) if (binding.camera != null) Undo.RecordObject(binding.camera, "Compare world grading");
        root.gameObject.SetActive(active);
        EditorSceneManager.MarkSceneDirty(scene); SceneView.RepaintAll(); Selection.activeGameObject = root.gameObject;
        Debug.Log("[Lighting Palette] " + (active ? "已启用新版光照与植被调色" : "已恢复本次修改前的光照与植被") + "，天空盒版本与昼夜周期不变。请保存场景。");
    }

    public static void InspectLightingAndVegetation()
    {
        EnsureSceneBackup(LightingPaletteBackup);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(LightingPalettePreviews);
        var log = new StringBuilder();
        log.AppendLine("Quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + " colorSpace=" + QualitySettings.activeColorSpace);
        log.AppendLine("Pipeline=" + AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline));
        var roots = scene.GetRootGameObjects();
        foreach (Transform transform in roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                log.AppendLine("EXISTING_MISSING_SCRIPT " + HierarchyPath(transform));
        foreach (Camera camera in roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
        {
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            log.AppendLine("CAM " + camera.name + " position=" + Format(camera.transform.position) + " near=" + camera.nearClipPlane + " far=" + camera.farClipPlane + " post=" + (data != null && data.renderPostProcessing) + " mask=" + (data == null ? 0 : data.volumeLayerMask.value));
        }
        foreach (Volume volume in roots.SelectMany(r => r.GetComponentsInChildren<Volume>(true)))
            log.AppendLine("VOLUME " + volume.name + " active=" + volume.isActiveAndEnabled + " global=" + volume.isGlobal + " priority=" + volume.priority + " profile=" + AssetDatabase.GetAssetPath(volume.sharedProfile));
        foreach (Light light in roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)))
            log.AppendLine("LIGHT " + HierarchyPath(light.transform) + " active=" + light.isActiveAndEnabled + " type=" + light.type + " intensity=" + light.intensity + " range=" + light.range + " color=" + light.color + " shadows=" + light.shadows + " position=" + Format(light.transform.position));
        foreach (Terrain terrain in roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)))
        {
            TerrainData data = terrain.terrainData;
            log.AppendLine("TERRAIN " + terrain.name + " data=" + AssetDatabase.GetAssetPath(data) + " size=" + data.size + " trees=" + data.treeInstanceCount + " detail=" + data.detailResolution + " material=" + AssetDatabase.GetAssetPath(terrain.materialTemplate));
            foreach (TerrainLayer layer in data.terrainLayers)
                if (layer != null) log.AppendLine(" LAYER " + layer.name + " path=" + AssetDatabase.GetAssetPath(layer) + " diffuse=" + AssetDatabase.GetAssetPath(layer.diffuseTexture) + " remap=" + layer.diffuseRemapMin + ".." + layer.diffuseRemapMax);
            foreach (TreePrototype tree in data.treePrototypes)
            {
                log.AppendLine(" TREE " + AssetDatabase.GetAssetPath(tree.prefab));
                if (tree.prefab != null) foreach (Renderer renderer in tree.prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials) DescribePaletteMaterial(log, material, "   ");
            }
            foreach (DetailPrototype detail in data.detailPrototypes)
                log.AppendLine(" DETAIL " + (detail.prototype == null ? AssetDatabase.GetAssetPath(detail.prototypeTexture) : AssetDatabase.GetAssetPath(detail.prototype)) + " mode=" + detail.renderMode + " colors=" + detail.healthyColor + "/" + detail.dryColor);
        }
        var renderers = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToArray();
        foreach (var group in renderers.SelectMany(r => r.sharedMaterials.Where(m => m != null).Select(m => new { renderer = r, material = m })).GroupBy(p => p.material).OrderByDescending(g => g.Count()))
        {
            DescribePaletteMaterial(log, group.Key, "MATERIAL count=" + group.Count() + " ");
            foreach (var item in group.Take(3)) log.AppendLine(" use=" + HierarchyPath(item.renderer.transform));
        }
        File.WriteAllText(LightingPalettePreviews + "/inventory-before.txt", log.ToString(), Encoding.UTF8);
        RenderPaletteView(LightingPalettePreviews + "/before-overview.png", new Vector3(332, 105, -52), new Vector3(247, 10, 65), 54f);
        RenderPaletteView(LightingPalettePreviews + "/before-dragon.png", new Vector3(202.8f, 116, 107), new Vector3(307.8f, 29, 232), 48f);
        RenderPaletteView(LightingPalettePreviews + "/before-base.png", new Vector3(212, 46, 43), new Vector3(173, 5, -43), 50f);
        Debug.Log("[Lighting Palette] Inventory and original scene previews ready: " + Path.GetFullPath(LightingPalettePreviews));
        if (Application.isBatchMode) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    private static string HierarchyPath(Transform transform) => transform.parent == null ? transform.name : HierarchyPath(transform.parent) + "/" + transform.name;

    private static void DescribePaletteMaterial(StringBuilder log, Material material, string prefix)
    {
        if (material == null) { log.AppendLine(prefix + "NULL"); return; }
        log.Append(prefix).Append(AssetDatabase.GetAssetPath(material)).Append(" shader=").Append(material.shader.name);
        foreach (string property in new[] { "_BaseColor", "_Color", "_EmissionColor", "_ShadeColor", "_SpecColor", "_HealthyColor", "_DryColor" })
            if (material.HasProperty(property)) log.Append(" ").Append(property).Append("=").Append(material.GetColor(property));
        foreach (string property in new[] { "_BaseMap", "_MainTex", "_ColorMap", "_EmissionMap" })
            if (material.HasProperty(property)) log.Append(" ").Append(property).Append("=").Append(AssetDatabase.GetAssetPath(material.GetTexture(property)));
        log.AppendLine();
    }

    private static void RenderPaletteView(string output, Vector3 position, Vector3 target, float fov)
    {
        var go = new GameObject("LightingPalettePreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>();
        camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = fov; camera.nearClipPlane = .3f; camera.farClipPlane = 1200;
        camera.allowHDR = true;
        var extra = camera.GetUniversalAdditionalCameraData();
        var art = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AerospaceLightingVegetationProfile>(true)).FirstOrDefault();
        AerospaceLightingVegetationProfile.AssociatePreviewCamera(camera, art);
        Debug.Log("[Palette Render] cameraScene=" + camera.gameObject.scene.handle + " activeScene=" + SceneManager.GetActiveScene().handle + " art=" + (AerospaceLightingVegetationProfile.ForCamera(camera) != null));
        extra.renderPostProcessing = true; extra.volumeLayerMask = ~0;
        extra.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        extra.antialiasingQuality = AntialiasingQuality.Medium;
        RenderTexture buffer = RenderTexture.GetTemporary(1600, 900, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = buffer;
            camera.Render();
            RenderTexture.active = buffer;
            pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
            File.WriteAllBytes(output, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            AerospaceLightingVegetationProfile.AssociatePreviewCamera(camera, null);
            RenderTexture.ReleaseTemporary(buffer); Object.DestroyImmediate(pixels); Object.DestroyImmediate(go);
        }
    }
}
