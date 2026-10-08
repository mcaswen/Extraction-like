using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ExtractionLike.Editor
{
    /// <summary>
    /// Keeps the curated Kenney aerospace models on URP/Lit materials.
    /// The source FBX files use legacy/imported materials, which can render pink in URP.
    /// </summary>
    [InitializeOnLoad]
    public static class AerospaceAssetSetup
    {
        private const string Root = "Assets/Art/External/AerospaceKit_CC0";
        private const string SpaceModels = Root + "/Models/Kenney_SpaceKit";
        private const string ModularModels = Root + "/Models/Kenney_ModularSpaceKit";
        private const string Materials = Root + "/Materials";
        private const string ModularColorMap = Root + "/Textures/Kenney_ModularSpaceKit/colormap.png";

        private static bool setupQueued;

        static AerospaceAssetSetup()
        {
            QueueSetup();
        }

        [MenuItem("Tools/Extraction-like/Aerospace/Refresh URP Materials")]
        private static void RefreshFromMenu()
        {
            EnsureSetup(true);
        }

        // Public entry point used by batch-mode validation and CI.
        public static void RefreshForBatchmode()
        {
            EnsureSetup(true);
        }

        private static void QueueSetup()
        {
            if (setupQueued)
            {
                return;
            }

            setupQueued = true;
            EditorApplication.delayCall += RunDelayedSetup;
        }

        private static void RunDelayedSetup()
        {
            setupQueued = false;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueSetup();
                return;
            }

            EnsureSetup(false);
        }

        private static void EnsureSetup(bool reportResult)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Aerospace Kit] URP/Lit shader was not found. The imported FBX files were left unchanged.");
                return;
            }

            EnsureFolder(Materials);
            ConfigureColorMap();

            var spaceMaterials = new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                { "_defaultMat", EnsureMaterial("M_Kenney_Default", shader, new Color(0.95f, 0.96f, 0.98f), 0.10f, 0.35f) },
                { "metal", EnsureMaterial("M_Kenney_Metal", shader, new Color(0.8431373f, 0.8705882f, 0.9098039f), 0.65f, 0.60f) },
                { "metalDark", EnsureMaterial("M_Kenney_MetalDark", shader, new Color(0.6750623f, 0.7100219f, 0.7735849f), 0.55f, 0.50f) },
                { "metalRed", EnsureMaterial("M_Kenney_Orange", shader, new Color(1.0f, 0.6285242f, 0.2028302f), 0.25f, 0.40f) },
                { "dark", EnsureMaterial("M_Kenney_Dark", shader, new Color(0.2745098f, 0.2980392f, 0.3411765f), 0.45f, 0.35f) }
            };

            Texture2D colorMap = AssetDatabase.LoadAssetAtPath<Texture2D>(ModularColorMap);
            var modularMaterials = new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                { "colormap", EnsureMaterial("M_Kenney_Modular_Colormap", shader, Color.white, 0.10f, 0.35f, colorMap) }
            };

            int changedModels = 0;
            changedModels += RemapModels(SpaceModels, spaceMaterials);
            changedModels += RemapModels(ModularModels, modularMaterials);
            AssetDatabase.SaveAssets();

            if (reportResult || changedModels > 0)
            {
                Debug.Log($"[Aerospace Kit] URP materials are ready. Updated {changedModels} model importer(s).");
            }
        }

        private static void ConfigureColorMap()
        {
            TextureImporter importer = AssetImporter.GetAtPath(ModularColorMap) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(ModularColorMap, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(ModularColorMap) as TextureImporter;
            }

            if (importer == null)
            {
                return;
            }

            bool changed = false;
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static Material EnsureMaterial(
            string materialName,
            Shader shader,
            Color color,
            float metallic,
            float smoothness,
            Texture texture = null)
        {
            string path = $"{Materials}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
            }

            bool changed = false;
            if (material.shader != shader)
            {
                material.shader = shader;
                changed = true;
            }

            changed |= SetColor(material, "_BaseColor", color);
            changed |= SetColor(material, "_Color", color);
            changed |= SetFloat(material, "_Metallic", metallic);
            changed |= SetFloat(material, "_Smoothness", smoothness);
            changed |= SetTexture(material, "_BaseMap", texture);
            changed |= SetTexture(material, "_MainTex", texture);

            if (!material.enableInstancing)
            {
                material.enableInstancing = true;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static int RemapModels(string folder, IReadOnlyDictionary<string, Material> materials)
        {
            int changedModels = 0;
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { folder });

            foreach (string guid in modelGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetExtension(assetPath), ".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                Dictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> current = importer.GetExternalObjectMap();
                bool changed = false;

                foreach (KeyValuePair<string, Material> pair in materials)
                {
                    var source = new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key);
                    if (!current.TryGetValue(source, out UnityEngine.Object mapped) || mapped != pair.Value)
                    {
                        importer.AddRemap(source, pair.Value);
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    changedModels++;
                }
            }

            return changedModels;
        }

        private static bool SetColor(Material material, string property, Color value)
        {
            if (!material.HasProperty(property) || Approximately(material.GetColor(property), value))
            {
                return false;
            }

            material.SetColor(property, value);
            return true;
        }

        private static bool SetFloat(Material material, string property, float value)
        {
            if (!material.HasProperty(property) || Mathf.Approximately(material.GetFloat(property), value))
            {
                return false;
            }

            material.SetFloat(property, value);
            return true;
        }

        private static bool SetTexture(Material material, string property, Texture value)
        {
            if (!material.HasProperty(property) || material.GetTexture(property) == value)
            {
                return false;
            }

            material.SetTexture(property, value);
            return true;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r)
                && Mathf.Approximately(a.g, b.g)
                && Mathf.Approximately(a.b, b.b)
                && Mathf.Approximately(a.a, b.a);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            if (!string.IsNullOrEmpty(parent))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
