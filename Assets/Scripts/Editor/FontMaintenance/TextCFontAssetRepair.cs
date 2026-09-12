using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>显式维护 text-c 的派生 TMP 缓存，不在导入或运行时自动改写资产。</summary>
public static class TextCFontAssetRepair
{
    private const string SourcePath = "Assets/Font/text-c.ttf";
    private const string CachePath = "Assets/Font/text-c SDF.asset";

    // Batch entry: -executeMethod TextCFontAssetRepair.Rebuild -quit
    public static void Rebuild()
    {
        AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceUpdate);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CachePath);
        if (font == null || font.sourceFontFile != AssetDatabase.LoadAssetAtPath<Font>(SourcePath))
            throw new InvalidOperationException("Unexpected text-c source font binding");
        if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            throw new InvalidOperationException("Expected the existing dynamic text-c cache");

        string guid = AssetDatabase.AssetPathToGUID(CachePath);
        long materialId = LocalId(font.material);
        long atlasId = LocalId(font.atlasTextures[0]);
        bool atlasReadable = font.atlasTextures[0].isReadable;
        uint[] characters = font.characterTable.Select(c => c.unicode).Distinct().ToArray();
        var dimensions = new Vector2Int(font.atlasWidth, font.atlasHeight);
        int pointSize = font.faceInfo.pointSize;
        // The existing atlas was packed offline. Repacking all 3,631 characters
        // with the dynamic packer overflows it, so preserve all other glyphs/UVs.
        const uint targetUnicode = 0x6307;
        var oldCharacter = font.characterTable.Single(c => c.unicode == targetUnicode);
        var oldGlyph = font.glyphTable.Single(g => g.index == oldCharacter.glyphIndex);
        if (font.atlasRenderMode != GlyphRenderMode.SDFAA)
            throw new InvalidOperationException("Review render flags before changing the text-c atlas mode");
        FontEngine.InitializeFontEngine();
        if (FontEngine.LoadFontFace(font.sourceFontFile, pointSize) != FontEngineError.Success ||
            !FontEngine.TryGetGlyphWithUnicodeValue(targetUnicode,
                GlyphLoadFlags.LOAD_NO_BITMAP | GlyphLoadFlags.LOAD_NO_HINTING, out var sourceGlyph))
            throw new InvalidOperationException("Could not load the repaired source glyph");
        // This repair changes the known bad glyph's bounds. Avoid consuming atlas
        // space again when explicitly rerunning the same maintenance command.
        if (oldGlyph.index == sourceGlyph.index && oldGlyph.metrics.Equals(sourceGlyph.metrics))
        {
            Debug.Log("text-c target glyph already matches the repaired source; no asset changes");
            return;
        }
        if (font.characterTable.Any(c => c.unicode != targetUnicode && c.glyphIndex == oldGlyph.index))
            throw new InvalidOperationException("Target glyph is shared by another character");
        int characterPosition = font.characterTable.IndexOf(oldCharacter);
        int glyphPosition = font.glyphTable.IndexOf(oldGlyph);
        font.characterTable.RemoveAt(characterPosition);
        font.glyphTable.RemoveAt(glyphPosition);
        font.ReadFontAssetDefinition();
        bool added = font.TryAddCharacters(new[] { targetUnicode }, out uint[] missing, false);
        if (!added || (missing != null && missing.Length != 0))
            throw new InvalidOperationException("Rebuild lost characters: " + string.Join(",", missing ?? new uint[0]));
        var newCharacter = font.characterTable.Single(c => c.unicode == targetUnicode);
        var newGlyph = font.glyphTable.Single(g => g.index == newCharacter.glyphIndex);
        font.characterTable.Remove(newCharacter);
        font.characterTable.Insert(characterPosition, newCharacter);
        font.glyphTable.Remove(newGlyph);
        font.glyphTable.Insert(glyphPosition, newGlyph);
        font.ReadFontAssetDefinition();
        if (characters.Any(c => !font.characterLookupTable.ContainsKey(c)) ||
            font.glyphTable.All(g => g.index != font.characterLookupTable[0x6307].glyphIndex))
            throw new InvalidOperationException("Rebuild left missing characters or glyphs");
        if (guid != AssetDatabase.AssetPathToGUID(CachePath) || materialId != LocalId(font.material) ||
            atlasId != LocalId(font.atlasTextures[0]) ||
            dimensions != new Vector2Int(font.atlasWidth, font.atlasHeight) || pointSize != font.faceInfo.pointSize)
            throw new InvalidOperationException("Rebuild changed cache identity or layout configuration");
        if (font.atlasTextures.Length != 1 || newGlyph.index != oldGlyph.index)
            throw new InvalidOperationException("Repair must retain the existing atlas and glyph index");

        EditorUtility.SetDirty(font);
        var serializedAtlas = new SerializedObject(font.atlasTextures[0]);
        serializedAtlas.FindProperty("m_IsReadable").boolValue = atlasReadable;
        serializedAtlas.ApplyModifiedPropertiesWithoutUndo();
        foreach (Texture2D atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/TextCFontRepair.json", JsonUtility.ToJson(new RepairReport {
            status = "PASS", characterCount = characters.Length, glyphCount = font.glyphTable.Count,
            fontGuid = guid, materialFileId = materialId, atlasFileId = atlasId,
            pointSize = pointSize, atlasWidth = dimensions.x, atlasHeight = dimensions.y
        }, true));
        Debug.Log("text-c TMP cache rebuilt, original characters and resource identities preserved");
    }

    private static long LocalId(UnityEngine.Object asset)
    {
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long id))
            throw new InvalidOperationException("Expected a persistent font subasset");
        return id;
    }

    [Serializable]
    private sealed class RepairReport
    {
        public string status;
        public int characterCount, glyphCount, pointSize, atlasWidth, atlasHeight;
        public string fontGuid;
        public long materialFileId, atlasFileId;
    }
}
