using System;
using System.IO;
using System.Linq;
using ExtractionLike.Aerospace;
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
            + string.Join("", Directory.GetFiles("Assets/Scripts/Gameplay/Aerospace", "*.cs").Select(File.ReadAllText));
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
