using System;
using System.IO;
using System.Linq;
using System.Text;
using ExtractionLike.Aerospace;
using Gameplay.MapGraph.Config;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class AerospaceScienceTypographyBuilder
{
    [MenuItem("Tools/Aerospace/Rebuild science interface typography")]
    public static void Build()
    {
        const string folder = "Assets/Resources/Aerospace/Fonts";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources/Aerospace", "Fonts");
        var presentation = AssetDatabase.LoadAssetAtPath<AerospacePresentationAssets>("Assets/Resources/Aerospace/Presentation.asset");
        if (presentation == null || presentation.bodyFont == null || presentation.headingFont == null) throw new InvalidOperationException("Missing science UI source fonts.");
        string characters = File.ReadAllText("Assets/Resources/Aerospace/PartsCatalog.json")
            + string.Join("", Directory.GetFiles("Assets/Scripts/Gameplay/Aerospace", "*.cs").Select(File.ReadAllText))
            + BrainPanelCharacters();
        characters = new string(characters.Where(c => c >= 32).Distinct().ToArray());
        presentation.inspectionBody = FontAsset(presentation.bodyFont, folder + "/ScienceBody.asset", characters, 2048);
        presentation.inspectionHeading = FontAsset(presentation.headingFont, folder + "/ScienceHeading.asset", characters, 2048);
        var serial = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/AerospaceCollectibles/Fonts/JetBrainsMono-Regular.ttf");
        if (serial == null) throw new InvalidOperationException("Missing OFL-licensed terminal font.");
        presentation.monoFont = serial;
        presentation.inspectionMono = FontAsset(serial, folder + "/ScienceTerminalSerial.asset", new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()), 1024);
        EditorUtility.SetDirty(presentation); AssetDatabase.SaveAssets();
        Debug.Log("SCIENCE_TYPOGRAPHY_PASS: three private SDF font assets; no global TMP settings or scene changes.");
    }
    /// <summary>
    /// 大脑面板和现场导演会显示的字符：遥测目录（含面板）、行为树目录（节点中文名写在树构建代码里）、导演目录，以及地图定义里的区域名
    /// </summary>
    public static string BrainPanelCharacters()
    {
        var text = new StringBuilder();
        foreach (string folder in new[] { "Assets/Scripts/Gameplay/Agent/Telemetry", "Assets/Scripts/Gameplay/Agent/AI", "Assets/Scripts/Gameplay/Demo" })
        {
            if (!Directory.Exists(folder)) continue;
            foreach (string file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories)) text.Append(File.ReadAllText(file));
        }

        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(SO_MapGraphDefinition)))
        {
            var map = AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (map == null) continue;
            foreach (var zone in map.Zones)
                if (zone != null) text.Append(zone.DisplayName);
        }

        return text.ToString();
    }

    private static TMP_FontAsset FontAsset(Font source, string path, string characters, int size)
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset == null)
        {
            asset = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, size, size, AtlasPopulationMode.Dynamic, true);
            if (asset == null) throw new InvalidOperationException("Cannot create font: " + source.name);
            asset.name = Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(asset, path);
            asset.material.name = asset.name + " Material"; AssetDatabase.AddObjectToAsset(asset.material, asset);
        }
        asset.TryAddCharacters(characters, out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("Science font missing characters: " + missing);
        foreach (var texture in asset.atlasTextures) if (texture != null && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, asset);
        EditorUtility.SetDirty(asset); return asset;
    }
}
