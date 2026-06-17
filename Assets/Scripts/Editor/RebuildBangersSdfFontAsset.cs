using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public sealed class RebuildBangersSdfFontAssetWindow : EditorWindow
{
    private const string WindowTitle = "Bangers SDF";
    private string _lastResult = "Ready.";
    private MessageType _lastResultType = MessageType.Info;

    [MenuItem("Tools/TextMesh Pro/Bangers SDF Rebuilder")]
    public static void Open()
    {
        RebuildBangersSdfFontAssetWindow window = GetWindow<RebuildBangersSdfFontAssetWindow>(true, WindowTitle);
        window.minSize = new Vector2(460, 210);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Bangers SDF Rebuilder", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Font Asset", BangersSdfFontAssetRebuilder.FontAssetPath);
            EditorGUILayout.TextField("Source Font", BangersSdfFontAssetRebuilder.SourceFontPath);
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Rebuilds the existing TMP FontAsset in place, keeping the .meta GUID and prefab references intact.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        if (GUILayout.Button("Rebuild / Overwrite Bangers SDF.asset", GUILayout.Height(34)))
        {
            try
            {
                BangersSdfFontAssetRebuilder.Rebuild();
                _lastResult = "Done. Bangers SDF.asset was rebuilt and validated.";
                _lastResultType = MessageType.Info;
                EditorUtility.DisplayDialog("Bangers SDF", _lastResult, "OK");
            }
            catch (Exception exception)
            {
                _lastResult = exception.Message;
                _lastResultType = MessageType.Error;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Bangers SDF rebuild failed", exception.Message, "OK");
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(_lastResult, _lastResultType);
    }
}

public static class BangersSdfFontAssetRebuilder
{
    public const string FontAssetPath = "Assets/ThirdParty/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset";
    public const string SourceFontPath = "Assets/ThirdParty/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf";

    [MenuItem("Tools/TextMesh Pro/Rebuild Bangers SDF Now")]
    public static void RebuildFromMenu()
    {
        try
        {
            Rebuild();
            EditorUtility.DisplayDialog("Bangers SDF", "Done. Bangers SDF.asset was rebuilt and validated.", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Bangers SDF rebuild failed", exception.Message, "OK");
        }
    }

    public static void Rebuild()
    {
        EditorUtility.DisplayProgressBar("Bangers SDF", "Loading font assets...", 0.1f);

        try
        {
            AssetDatabase.Refresh();

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);

            if (fontAsset == null)
                throw new InvalidOperationException($"Could not load TMP font asset at {FontAssetPath}.");

            if (sourceFont == null)
                throw new InvalidOperationException($"Could not load source font at {SourceFontPath}.");

            string sourceFontGuid = AssetDatabase.AssetPathToGUID(SourceFontPath);
            string fontAssetGuid = AssetDatabase.AssetPathToGUID(FontAssetPath);
            uint[] charactersToRebuild = GetCharactersToRebuild(fontAsset);

            int pointSize = fontAsset.creationSettings.pointSize > 0
                ? fontAsset.creationSettings.pointSize
                : Mathf.Max(1, Mathf.RoundToInt(fontAsset.faceInfo.pointSize));

            int atlasPadding = fontAsset.creationSettings.padding > 0 ? fontAsset.creationSettings.padding : fontAsset.atlasPadding;
            int atlasWidth = fontAsset.creationSettings.atlasWidth > 0 ? fontAsset.creationSettings.atlasWidth : fontAsset.atlasWidth;
            int atlasHeight = fontAsset.creationSettings.atlasHeight > 0 ? fontAsset.creationSettings.atlasHeight : fontAsset.atlasHeight;
            GlyphRenderMode renderMode = fontAsset.creationSettings.renderMode != 0
                ? (GlyphRenderMode)fontAsset.creationSettings.renderMode
                : fontAsset.atlasRenderMode;

            EditorUtility.DisplayProgressBar("Bangers SDF", "Clearing old TMP font data...", 0.35f);
            ApplySerializedConfiguration(fontAsset, sourceFont, sourceFontGuid, atlasPadding, atlasWidth, atlasHeight, renderMode);

            FontEngine.InitializeFontEngine();
            FontEngineError loadResult = FontEngine.LoadFontFace(sourceFont, pointSize);
            if (loadResult != FontEngineError.Success)
                throw new InvalidOperationException($"FontEngine failed to load {SourceFontPath} at point size {pointSize}: {loadResult}.");

            fontAsset.faceInfo = FontEngine.GetFaceInfo();
            fontAsset.ClearFontAssetData(true);
            ApplySerializedConfiguration(fontAsset, sourceFont, sourceFontGuid, atlasPadding, atlasWidth, atlasHeight, renderMode);

            EditorUtility.DisplayProgressBar("Bangers SDF", "Regenerating glyph atlas...", 0.65f);
            bool addedAllCharacters = fontAsset.TryAddCharacters(charactersToRebuild, out uint[] missingUnicodes, false);
            if (!addedAllCharacters && missingUnicodes != null && missingUnicodes.Length > 0)
                Debug.LogWarning($"Bangers SDF rebuild skipped missing Unicode values: {string.Join(", ", missingUnicodes)}", fontAsset);

            UpdateCreationSettings(fontAsset, sourceFontGuid, fontAssetGuid, pointSize, atlasPadding, atlasWidth, atlasHeight, renderMode);
            RefreshMaterial(fontAsset, atlasPadding, atlasWidth, atlasHeight, renderMode);

            EditorUtility.DisplayProgressBar("Bangers SDF", "Validating rebuilt font asset...", 0.9f);
            fontAsset.ReadFontAssetDefinition();
            ValidateNoDanglingGlyphReferences(fontAsset);
            ValidateUnicode(fontAsset, 51, "3");
            ValidateUnicode(fontAsset, 67, "C");

            MarkDirty(fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            Debug.Log($"Rebuilt {FontAssetPath} with {fontAsset.characterTable.Count} characters and {fontAsset.glyphTable.Count} glyphs.", fontAsset);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static uint[] GetCharactersToRebuild(TMP_FontAsset fontAsset)
    {
        uint[] characters = fontAsset.characterTable
            .Where(character => character != null)
            .Select(character => character.unicode)
            .Distinct()
            .OrderBy(unicode => unicode)
            .ToArray();

        if (characters.Length > 0)
            return characters;

        return Enumerable.Range(32, 95).Select(value => (uint)value).ToArray();
    }

    private static void ApplySerializedConfiguration(
        TMP_FontAsset fontAsset,
        Font sourceFont,
        string sourceFontGuid,
        int atlasPadding,
        int atlasWidth,
        int atlasHeight,
        GlyphRenderMode renderMode)
    {
        SerializedObject serializedFontAsset = new SerializedObject(fontAsset);

        serializedFontAsset.FindProperty("m_Version").stringValue = "1.1.0";
        serializedFontAsset.FindProperty("m_SourceFontFileGUID").stringValue = sourceFontGuid;
        serializedFontAsset.FindProperty("m_SourceFontFile_EditorRef").objectReferenceValue = sourceFont;
        serializedFontAsset.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
        serializedFontAsset.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Dynamic;
        serializedFontAsset.FindProperty("m_AtlasWidth").intValue = atlasWidth;
        serializedFontAsset.FindProperty("m_AtlasHeight").intValue = atlasHeight;
        serializedFontAsset.FindProperty("m_AtlasPadding").intValue = atlasPadding;
        serializedFontAsset.FindProperty("m_AtlasRenderMode").intValue = (int)renderMode;

        serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void UpdateCreationSettings(
        TMP_FontAsset fontAsset,
        string sourceFontGuid,
        string fontAssetGuid,
        int pointSize,
        int atlasPadding,
        int atlasWidth,
        int atlasHeight,
        GlyphRenderMode renderMode)
    {
        FontAssetCreationSettings settings = fontAsset.creationSettings;

        settings.sourceFontFileGUID = sourceFontGuid;
        settings.pointSizeSamplingMode = 1;
        settings.pointSize = pointSize;
        settings.padding = atlasPadding;
        settings.packingMode = 4;
        settings.atlasWidth = atlasWidth;
        settings.atlasHeight = atlasHeight;
        settings.characterSetSelectionMode = 6;
        settings.characterSequence = string.Empty;
        settings.referencedFontAssetGUID = fontAssetGuid;
        settings.referencedTextAssetGUID = string.Empty;
        settings.fontStyle = 0;
        settings.fontStyleModifier = 0;
        settings.renderMode = (int)renderMode;
        settings.includeFontFeatures = false;

        fontAsset.creationSettings = settings;
    }

    private static void RefreshMaterial(
        TMP_FontAsset fontAsset,
        int atlasPadding,
        int atlasWidth,
        int atlasHeight,
        GlyphRenderMode renderMode)
    {
        if (fontAsset.material == null || fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0)
            return;

        int packingModifier = IsBitmapRenderMode(renderMode) ? 0 : 1;

        fontAsset.material.SetTexture("_MainTex", fontAsset.atlasTextures[0]);
        fontAsset.material.SetFloat("_TextureWidth", atlasWidth);
        fontAsset.material.SetFloat("_TextureHeight", atlasHeight);
        fontAsset.material.SetFloat("_GradientScale", atlasPadding + packingModifier);
        fontAsset.material.SetFloat("_WeightNormal", fontAsset.normalStyle);
        fontAsset.material.SetFloat("_WeightBold", fontAsset.boldStyle);
    }

    private static bool IsBitmapRenderMode(GlyphRenderMode renderMode)
    {
        return renderMode == GlyphRenderMode.RASTER || renderMode == GlyphRenderMode.RASTER_HINTED;
    }

    private static void ValidateNoDanglingGlyphReferences(TMP_FontAsset fontAsset)
    {
        HashSet<uint> glyphIndexes = new HashSet<uint>(fontAsset.glyphTable.Select(glyph => glyph.index));

        foreach (TMP_Character character in fontAsset.characterTable)
        {
            if (!glyphIndexes.Contains(character.glyphIndex))
                throw new InvalidOperationException($"Unicode {character.unicode} points to missing glyph index {character.glyphIndex}.");
        }
    }

    private static void ValidateUnicode(TMP_FontAsset fontAsset, uint unicode, string label)
    {
        TMP_Character character = fontAsset.characterTable.FirstOrDefault(candidate => candidate.unicode == unicode);

        if (character == null)
            throw new InvalidOperationException($"Unicode {unicode} ({label}) was not rebuilt into {FontAssetPath}.");

        if (character.glyph == null)
            throw new InvalidOperationException($"Unicode {unicode} ({label}) was rebuilt without a glyph reference.");
    }

    private static void MarkDirty(TMP_FontAsset fontAsset)
    {
        EditorUtility.SetDirty(fontAsset);

        if (fontAsset.material != null)
            EditorUtility.SetDirty(fontAsset.material);

        if (fontAsset.atlasTextures == null)
            return;

        foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
        {
            if (atlasTexture != null)
                EditorUtility.SetDirty(atlasTexture);
        }
    }
}
