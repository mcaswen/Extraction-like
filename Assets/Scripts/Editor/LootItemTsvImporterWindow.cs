using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class LootItemTsvImporterWindow : EditorWindow
{
    private const string DefaultTsvPath = "Assets/Config/Loot/LootItems.tsv";
    private const string ItemDataFolder = "Assets/SO/ItemData/Table";
    private const string WorldPrefabFolder = "Assets/Prefabs/ItemPrefabIn3D";
    private const string GeneratedArtFolder = "Assets/Art/Generated/Loot";
    private const string DefaultIconPath = GeneratedArtFolder + "/DefaultLootIcon.png";
    private const string DefaultMaterialPath = GeneratedArtFolder + "/M_LootWhitebox.mat";
    private const int MaxPreviewMessages = 24;

    private static readonly Regex ItemIdPattern = new Regex("^[a-z0-9_]+$", RegexOptions.Compiled);

    private string _tsvPath = DefaultTsvPath;
    private Vector2 _scrollPosition;
    private LootImportReport _lastReport;

    [MenuItem("Tools/Backpack/Import Loot Items From TSV")]
    private static void OpenWindow()
    {
        GetWindow<LootItemTsvImporterWindow>("Loot TSV Importer");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Loot Item TSV Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Imports item definition rows from an Excel-exported TSV file. " +
            "The tool creates or updates InventoryItemData assets and ensures each item has a world pickup prefab. " +
            "Loot box contents are intentionally left for designers to configure manually.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            _tsvPath = EditorGUILayout.TextField("TSV Path", _tsvPath);
            if (GUILayout.Button("Browse", GUILayout.Width(72f)))
            {
                string absolutePath = EditorUtility.OpenFilePanel("Select Loot Items TSV", Application.dataPath, "tsv,txt");
                if (!string.IsNullOrWhiteSpace(absolutePath))
                {
                    _tsvPath = ToAssetPath(absolutePath);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate Only"))
            {
                _lastReport = RunImport(writeAssets: false);
            }

            if (GUILayout.Button("Import"))
            {
                _lastReport = RunImport(writeAssets: true);
            }

            if (GUILayout.Button("Ping Output Folder"))
            {
                UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ItemDataFolder);
                if (folder != null)
                {
                    EditorGUIUtility.PingObject(folder);
                    Selection.activeObject = folder;
                }
            }
        }

        DrawReport();
    }

    private void DrawReport()
    {
        if (_lastReport == null)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Last Report", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Mode", _lastReport.WroteAssets ? "Import" : "Validate Only");
        EditorGUILayout.LabelField("Valid Rows", _lastReport.ValidRows.ToString(CultureInfo.InvariantCulture));
        EditorGUILayout.LabelField("Skipped Rows", _lastReport.SkippedRows.ToString(CultureInfo.InvariantCulture));
        EditorGUILayout.LabelField("Created Items", _lastReport.CreatedItems.ToString(CultureInfo.InvariantCulture));
        EditorGUILayout.LabelField("Updated Items", _lastReport.UpdatedItems.ToString(CultureInfo.InvariantCulture));
        EditorGUILayout.LabelField("Created Prefabs", _lastReport.CreatedPrefabs.ToString(CultureInfo.InvariantCulture));
        EditorGUILayout.LabelField("Repaired Prefabs", _lastReport.RepairedPrefabs.ToString(CultureInfo.InvariantCulture));

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.MinHeight(160f));
        DrawMessages("Errors", _lastReport.Errors, MessageType.Error);
        DrawMessages("Warnings", _lastReport.Warnings, MessageType.Warning);
        DrawMessages("Info", _lastReport.Info, MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private static void DrawMessages(string title, List<string> messages, MessageType messageType)
    {
        if (messages == null || messages.Count == 0)
        {
            return;
        }

        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        int count = Mathf.Min(messages.Count, MaxPreviewMessages);
        for (int i = 0; i < count; i++)
        {
            EditorGUILayout.HelpBox(messages[i], messageType);
        }

        if (messages.Count > count)
        {
            EditorGUILayout.HelpBox($"Showing {count} of {messages.Count} messages. See Console for the full report.", MessageType.Info);
        }
    }

    private static LootImportReport RunImport(bool writeAssets)
    {
        LootImportReport report = new LootImportReport
        {
            WroteAssets = writeAssets
        };

        string absoluteTsvPath = ToAbsolutePath(DefaultTsvPath);
        LootItemTsvImporterWindow window = GetWindow<LootItemTsvImporterWindow>();
        if (window != null)
        {
            absoluteTsvPath = ToAbsolutePath(window._tsvPath);
        }

        if (!File.Exists(absoluteTsvPath))
        {
            report.Errors.Add($"TSV file does not exist: {absoluteTsvPath}");
            FinishReport(report);
            return report;
        }

        List<LootItemImportRow> rows = ReadRows(absoluteTsvPath, report);
        if (report.Errors.Count > 0)
        {
            FinishReport(report);
            return report;
        }

        report.ValidRows = rows.Count;
        if (!ValidateRows(rows, report))
        {
            FinishReport(report);
            return report;
        }

        Dictionary<string, InventoryItemData> existingItems = FindExistingItemsById(report);
        if (report.Errors.Count > 0)
        {
            FinishReport(report);
            return report;
        }

        ReportOrphanAssets(rows, existingItems, report);

        if (!writeAssets)
        {
            report.Info.Add("Validation completed. No assets were written.");
            FinishReport(report);
            return report;
        }

        EnsureAssetFolder(ItemDataFolder);
        EnsureAssetFolder(WorldPrefabFolder);
        EnsureAssetFolder(GeneratedArtFolder);

        Sprite defaultIcon = EnsureDefaultIcon();
        Material defaultMaterial = EnsureDefaultMaterial();

        foreach (LootItemImportRow row in rows)
        {
            ImportRow(row, existingItems, defaultIcon, defaultMaterial, report);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.Info.Add("Import completed.");
        FinishReport(report);
        return report;
    }

    private static List<LootItemImportRow> ReadRows(string absoluteTsvPath, LootImportReport report)
    {
        List<LootItemImportRow> rows = new List<LootItemImportRow>();
        string[] lines = File.ReadAllLines(absoluteTsvPath, Encoding.UTF8);
        if (lines.Length <= 1)
        {
            report.Errors.Add("TSV file must contain a header row and at least one data row.");
            return rows;
        }

        List<string> headers = SplitTsvLine(lines[0]);
        if (headers.Count > 0)
        {
            headers[0] = headers[0].TrimStart('\uFEFF');
        }
        Dictionary<string, int> headerIndexes = BuildHeaderMap(headers);
        string[] requiredHeaders =
        {
            "ItemID",
            "ItemName",
            "Type",
            "EquipmentKind",
            "Rarity",
            "Width",
            "Height",
            "IsStackable",
            "MaxStack",
            "ContainerColumns",
            "ContainerRows",
            "BlockedCells",
            "RequiresSearchInLootContainer",
            "SearchDurationOverride",
            "MagicUnlock",
            "RunePatternPoints",
            "Enabled",
            "SellPrice",
            "Notes"
        };

        foreach (string requiredHeader in requiredHeaders)
        {
            if (!headerIndexes.ContainsKey(requiredHeader))
            {
                report.Errors.Add($"Missing TSV column: {requiredHeader}");
            }
        }

        if (report.Errors.Count > 0)
        {
            return rows;
        }

        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            List<string> cells = SplitTsvLine(line);
            int rowNumber = lineIndex + 1;
            string enabledText = GetCell(cells, headerIndexes, "Enabled");
            if (!TryParseBoolean(enabledText, out bool isEnabled))
            {
                report.Errors.Add($"Row {rowNumber}: Enabled must be Yes/No or yes/no in Chinese.");
                continue;
            }

            if (!isEnabled)
            {
                report.SkippedRows++;
                continue;
            }

            LootItemImportRow row = new LootItemImportRow
            {
                RowNumber = rowNumber,
                ItemId = GetCell(cells, headerIndexes, "ItemID").Trim(),
                ItemName = GetCell(cells, headerIndexes, "ItemName").Trim(),
                TypeText = GetCell(cells, headerIndexes, "Type").Trim(),
                EquipmentKindText = GetCell(cells, headerIndexes, "EquipmentKind").Trim(),
                RarityText = GetCell(cells, headerIndexes, "Rarity").Trim(),
                WidthText = GetCell(cells, headerIndexes, "Width").Trim(),
                HeightText = GetCell(cells, headerIndexes, "Height").Trim(),
                IsStackableText = GetCell(cells, headerIndexes, "IsStackable").Trim(),
                MaxStackText = GetCell(cells, headerIndexes, "MaxStack").Trim(),
                ContainerColumnsText = GetCell(cells, headerIndexes, "ContainerColumns").Trim(),
                ContainerRowsText = GetCell(cells, headerIndexes, "ContainerRows").Trim(),
                BlockedCellsText = GetCell(cells, headerIndexes, "BlockedCells").Trim(),
                RequiresSearchText = GetCell(cells, headerIndexes, "RequiresSearchInLootContainer").Trim(),
                SearchDurationOverrideText = GetCell(cells, headerIndexes, "SearchDurationOverride").Trim(),
                MagicUnlockText = GetCell(cells, headerIndexes, "MagicUnlock").Trim(),
                RunePatternPointsText = GetCell(cells, headerIndexes, "RunePatternPoints").Trim(),
                CarryWeightText = GetOptionalCell(cells, headerIndexes, "CarryWeight", "1").Trim(),
                SellPriceText = GetCell(cells, headerIndexes, "SellPrice").Trim(),
                Notes = GetCell(cells, headerIndexes, "Notes").Trim()
            };
            rows.Add(row);
        }

        return rows;
    }

    private static bool ValidateRows(List<LootItemImportRow> rows, LootImportReport report)
    {
        HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < rows.Count; i++)
        {
            LootItemImportRow row = rows[i];
            ValidateRow(row, seenIds, report);
        }

        return report.Errors.Count == 0;
    }

    private static void ValidateRow(LootItemImportRow row, HashSet<string> seenIds, LootImportReport report)
    {
        if (string.IsNullOrWhiteSpace(row.ItemId))
        {
            report.Errors.Add($"Row {row.RowNumber}: ItemID is required.");
        }
        else
        {
            row.ItemId = row.ItemId.Trim();
            if (!ItemIdPattern.IsMatch(row.ItemId))
            {
                report.Errors.Add($"Row {row.RowNumber}: ItemID '{row.ItemId}' must use lowercase letters, numbers, and underscores only.");
            }

            if (!seenIds.Add(row.ItemId))
            {
                report.Errors.Add($"Row {row.RowNumber}: Duplicate ItemID '{row.ItemId}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(row.ItemName))
        {
            report.Errors.Add($"Row {row.RowNumber}: ItemName is required.");
        }

        if (!TryParseItemType(row.TypeText, out row.Type))
        {
            report.Errors.Add($"Row {row.RowNumber}: Type must be Bag/Rig/Equipment/Other or 背包/胸挂/装备/其他.");
        }

        if (!TryParseEquipmentKind(row.EquipmentKindText, out row.EquipmentKind))
        {
            report.Errors.Add($"Row {row.RowNumber}: Invalid EquipmentKind '{row.EquipmentKindText}'.");
        }

        if (!Enum.TryParse(row.RarityText, ignoreCase: true, out row.Rarity))
        {
            report.Errors.Add($"Row {row.RowNumber}: Invalid Rarity '{row.RarityText}'.");
        }

        if (!TryParseWhole(row.WidthText, out row.Width) || row.Width < 1 || row.Width > 10)
        {
            report.Errors.Add($"Row {row.RowNumber}: Width must be a whole number from 1 to 10.");
        }

        if (!TryParseWhole(row.HeightText, out row.Height) || row.Height < 1 || row.Height > 10)
        {
            report.Errors.Add($"Row {row.RowNumber}: Height must be a whole number from 1 to 10.");
        }

        if (!TryParseBoolean(row.IsStackableText, out row.IsStackable))
        {
            report.Errors.Add($"Row {row.RowNumber}: IsStackable must be Yes/No or yes/no in Chinese.");
        }

        if (!TryParseWhole(row.MaxStackText, out row.MaxStack) || row.MaxStack < 1)
        {
            report.Errors.Add($"Row {row.RowNumber}: MaxStack must be at least 1.");
        }

        if (!row.IsStackable && row.MaxStack != 1)
        {
            report.Errors.Add($"Row {row.RowNumber}: MaxStack must be 1 when IsStackable is No.");
        }

        bool isEquipment = row.Type == ItemType.Equipment;
        if (isEquipment)
        {
            if (row.EquipmentKind == EquipmentSlotKind.None)
            {
                report.Errors.Add($"Row {row.RowNumber}: EquipmentKind is required when Type is Equipment.");
            }
        }
        else
        {
            if (row.EquipmentKind != EquipmentSlotKind.None)
            {
                report.Errors.Add($"Row {row.RowNumber}: EquipmentKind must be None or blank when Type is not Equipment.");
            }

            row.EquipmentKind = EquipmentSlotKind.None;
        }

        bool isContainer = row.Type == ItemType.Bag || row.Type == ItemType.Rig;
        if (isContainer)
        {
            if (!TryParseWhole(row.ContainerColumnsText, out row.ContainerColumns) || row.ContainerColumns < 1)
            {
                report.Errors.Add($"Row {row.RowNumber}: ContainerColumns is required for Bag/Rig and must be at least 1.");
            }

            if (!TryParseWhole(row.ContainerRowsText, out row.ContainerRows) || row.ContainerRows < 1)
            {
                report.Errors.Add($"Row {row.RowNumber}: ContainerRows is required for Bag/Rig and must be at least 1.");
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(row.ContainerColumnsText) || !string.IsNullOrWhiteSpace(row.ContainerRowsText))
            {
                report.Errors.Add($"Row {row.RowNumber}: ContainerColumns and ContainerRows must be blank unless Type is Bag or Rig.");
            }

            row.ContainerColumns = 0;
            row.ContainerRows = 0;
        }

        if (!TryParseBlockedCells(row.BlockedCellsText, row.ContainerColumns, row.ContainerRows, isContainer, out row.BlockedCells, out string blockedCellError))
        {
            report.Errors.Add($"Row {row.RowNumber}: {blockedCellError}");
        }

        if (!TryParseBoolean(row.RequiresSearchText, out row.RequiresSearchInLootContainer))
        {
            report.Errors.Add($"Row {row.RowNumber}: RequiresSearchInLootContainer must be Yes/No or yes/no in Chinese.");
        }

        if (!float.TryParse(row.SearchDurationOverrideText, NumberStyles.Float, CultureInfo.InvariantCulture, out row.SearchDurationOverride) ||
            row.SearchDurationOverride < -1f)
        {
            report.Errors.Add($"Row {row.RowNumber}: SearchDurationOverride must be -1 or a non-negative number.");
        }
        else if (row.SearchDurationOverride < 0f && !Mathf.Approximately(row.SearchDurationOverride, -1f))
        {
            report.Errors.Add($"Row {row.RowNumber}: SearchDurationOverride must be exactly -1 or a non-negative number.");
        }

        if (!Enum.TryParse(row.MagicUnlockText, ignoreCase: true, out row.MagicUnlock))
        {
            report.Errors.Add($"Row {row.RowNumber}: Invalid MagicUnlock '{row.MagicUnlockText}'.");
        }

        if (!TryParseWhole(row.RunePatternPointsText, out row.RunePatternPoints) || row.RunePatternPoints < 1)
        {
            report.Errors.Add($"Row {row.RowNumber}: RunePatternPoints must be at least 1.");
        }

        if (!float.TryParse(row.CarryWeightText, NumberStyles.Float, CultureInfo.InvariantCulture, out row.CarryWeight) ||
            row.CarryWeight < 0f)
        {
            report.Errors.Add($"Row {row.RowNumber}: CarryWeight must be a non-negative number.");
        }

        if (!TryParseWhole(row.SellPriceText, out row.SellPrice) || row.SellPrice < 0)
        {
            report.Errors.Add($"Row {row.RowNumber}: SellPrice must be a non-negative whole number.");
        }
    }

    private static void ImportRow(
        LootItemImportRow row,
        Dictionary<string, InventoryItemData> existingItems,
        Sprite defaultIcon,
        Material defaultMaterial,
        LootImportReport report)
    {
        bool createdItem = false;
        if (!existingItems.TryGetValue(row.ItemId, out InventoryItemData itemData) || itemData == null)
        {
            itemData = ScriptableObject.CreateInstance<InventoryItemData>();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{ItemDataFolder}/{SanitizeFileName(row.ItemId)}.asset");
            AssetDatabase.CreateAsset(itemData, assetPath);
            existingItems[row.ItemId] = itemData;
            createdItem = true;
            report.CreatedItems++;
        }
        else
        {
            Undo.RecordObject(itemData, "Import Loot Item Data");
            report.UpdatedItems++;
        }

        bool hadNonDefaultIcon = itemData.ItemIcon != null && itemData.ItemIcon != defaultIcon;

        itemData.ItemID = row.ItemId;
        itemData.ItemName = row.ItemName;
        itemData.Type = row.Type;
        itemData.EquipmentKind = row.EquipmentKind;
        itemData.Rarity = row.Rarity;
        itemData.Width = row.Width;
        itemData.Height = row.Height;
        itemData.IsStackable = row.IsStackable;
        itemData.MaxStack = row.IsStackable ? row.MaxStack : 1;
        itemData.ContainerColumns = row.ContainerColumns;
        itemData.ContainerRows = row.ContainerRows;
        itemData.BlockedCells = new List<Vector2Int>(row.BlockedCells);
        itemData.RequiresSearchInLootContainer = row.RequiresSearchInLootContainer;
        itemData.SearchDurationOverride = row.SearchDurationOverride;
        itemData.MagicUnlock = row.MagicUnlock;
        itemData.RunePatternPoints = row.RunePatternPoints;
        itemData.CarryWeight = row.CarryWeight;
        itemData.SellPrice = row.SellPrice;

        if (!hadNonDefaultIcon && defaultIcon != null)
        {
            itemData.ItemIcon = defaultIcon;
        }

        GameObject worldPrefab = EnsureWorldPrefab(itemData, row, defaultMaterial, report);
        if (worldPrefab != null)
        {
            itemData.WorldPrefab = worldPrefab;
        }

        EditorUtility.SetDirty(itemData);
        if (createdItem)
        {
            report.Info.Add($"Created item asset: {row.ItemId}");
        }
    }

    private static GameObject EnsureWorldPrefab(
        InventoryItemData itemData,
        LootItemImportRow row,
        Material defaultMaterial,
        LootImportReport report)
    {
        GameObject existingPrefab = itemData.WorldPrefab;
        string existingPath = existingPrefab != null ? AssetDatabase.GetAssetPath(existingPrefab) : string.Empty;
        bool hasValidExistingPrefab = existingPrefab != null && !string.IsNullOrWhiteSpace(existingPath);
        string prefabPath = hasValidExistingPrefab
            ? existingPath
            : AssetDatabase.GenerateUniqueAssetPath($"{WorldPrefabFolder}/World_{SanitizeFileName(row.ItemId)}.prefab");

        if (!hasValidExistingPrefab)
        {
            GameObject prefabRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefabRoot.name = $"World_{row.ItemId}";
            prefabRoot.transform.position = Vector3.zero;
            prefabRoot.transform.rotation = Quaternion.identity;
            prefabRoot.transform.localScale = ResolveWhiteboxScale(row);

            Renderer rendererComponent = prefabRoot.GetComponent<Renderer>();
            if (rendererComponent != null && defaultMaterial != null)
            {
                rendererComponent.sharedMaterial = defaultMaterial;
            }

            WorldLootItem worldLootItem = prefabRoot.AddComponent<WorldLootItem>();
            worldLootItem.ItemData = itemData;
            worldLootItem.CurrentAmount = 1;

            Rigidbody rigidbody = prefabRoot.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            report.CreatedPrefabs++;
            return savedPrefab;
        }

        GameObject loadedRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            bool changed = false;
            WorldLootItem worldLootItem = loadedRoot.GetComponent<WorldLootItem>();
            if (worldLootItem == null)
            {
                worldLootItem = loadedRoot.AddComponent<WorldLootItem>();
                changed = true;
            }

            if (worldLootItem.ItemData != itemData)
            {
                worldLootItem.ItemData = itemData;
                changed = true;
            }

            if (worldLootItem.CurrentAmount < 1)
            {
                worldLootItem.CurrentAmount = 1;
                changed = true;
            }

            if (loadedRoot.GetComponentInChildren<Collider>(true) == null)
            {
                loadedRoot.AddComponent<BoxCollider>();
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(loadedRoot, prefabPath);
                report.RepairedPrefabs++;
                report.Info.Add($"Repaired world prefab runtime setup: {prefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(loadedRoot);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private static Dictionary<string, InventoryItemData> FindExistingItemsById(LootImportReport report)
    {
        Dictionary<string, InventoryItemData> itemsById = new Dictionary<string, InventoryItemData>(StringComparer.Ordinal);
        string[] guids = AssetDatabase.FindAssets("t:InventoryItemData", new[] { ItemDataFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            InventoryItemData itemData = AssetDatabase.LoadAssetAtPath<InventoryItemData>(path);
            if (itemData == null || string.IsNullOrWhiteSpace(itemData.ItemID))
            {
                continue;
            }

            if (itemsById.ContainsKey(itemData.ItemID))
            {
                report.Errors.Add($"Duplicate existing InventoryItemData ItemID '{itemData.ItemID}' found. Resolve this before importing.");
                continue;
            }

            itemsById[itemData.ItemID] = itemData;
        }

        return itemsById;
    }

    private static void ReportOrphanAssets(
        List<LootItemImportRow> rows,
        Dictionary<string, InventoryItemData> existingItems,
        LootImportReport report)
    {
        HashSet<string> sourceIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < rows.Count; i++)
        {
            sourceIds.Add(rows[i].ItemId);
        }

        foreach (KeyValuePair<string, InventoryItemData> pair in existingItems)
        {
            if (!sourceIds.Contains(pair.Key))
            {
                string path = AssetDatabase.GetAssetPath(pair.Value);
                report.Warnings.Add($"Existing item asset is absent from TSV and was not deleted: {pair.Key} ({path})");
            }
        }
    }

    private static Sprite EnsureDefaultIcon()
    {
        EnsureAssetFolder(GeneratedArtFolder);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultIconPath);
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        byte[] pngBytes = texture.EncodeToPNG();
        UnityEngine.Object.DestroyImmediate(texture);
        File.WriteAllBytes(ToAbsolutePath(DefaultIconPath), pngBytes);
        AssetDatabase.ImportAsset(DefaultIconPath);

        TextureImporter importer = AssetImporter.GetAtPath(DefaultIconPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(DefaultIconPath);
    }

    private static Material EnsureDefaultMaterial()
    {
        EnsureAssetFolder(GeneratedArtFolder);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(DefaultMaterialPath)
            };
            AssetDatabase.CreateAsset(material, DefaultMaterialPath);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.color = Color.white;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool TryParseItemType(string value, out ItemType type)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (string.Equals(normalized, "Bag", StringComparison.OrdinalIgnoreCase) ||
            normalized == "背包")
        {
            type = ItemType.Bag;
            return true;
        }

        if (string.Equals(normalized, "Rig", StringComparison.OrdinalIgnoreCase) ||
            normalized == "胸挂")
        {
            type = ItemType.Rig;
            return true;
        }

        if (string.Equals(normalized, "Equipment", StringComparison.OrdinalIgnoreCase) ||
            normalized == "装备")
        {
            type = ItemType.Equipment;
            return true;
        }

        if (string.Equals(normalized, "Other", StringComparison.OrdinalIgnoreCase) ||
            normalized == "其他")
        {
            type = ItemType.Junk;
            return true;
        }

        type = ItemType.Junk;
        return false;
    }

    private static bool TryParseEquipmentKind(string value, out EquipmentSlotKind kind)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            kind = EquipmentSlotKind.None;
            return true;
        }

        return Enum.TryParse(normalized, ignoreCase: true, out kind);
    }

    private static bool TryParseBoolean(string value, out bool result)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (string.Equals(normalized, "Yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "True", StringComparison.OrdinalIgnoreCase) ||
            normalized == "1" ||
            normalized == "是")
        {
            result = true;
            return true;
        }

        if (string.Equals(normalized, "No", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "False", StringComparison.OrdinalIgnoreCase) ||
            normalized == "0" ||
            normalized == "否")
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    private static bool TryParseWhole(string value, out int result)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseBlockedCells(
        string value,
        int columns,
        int rows,
        bool isContainer,
        out List<Vector2Int> blockedCells,
        out string error)
    {
        blockedCells = new List<Vector2Int>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!isContainer)
        {
            error = "BlockedCells must be blank unless Type is Bag or Rig.";
            return false;
        }

        HashSet<Vector2Int> uniqueCells = new HashSet<Vector2Int>();
        string[] entries = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < entries.Length; i++)
        {
            string[] parts = entries[i].Split(',');
            if (parts.Length != 2 ||
                !TryParseWhole(parts[0].Trim(), out int x) ||
                !TryParseWhole(parts[1].Trim(), out int y))
            {
                error = "BlockedCells must use the format x,y;x,y.";
                return false;
            }

            if (x < 0 || y < 0 || x >= columns || y >= rows)
            {
                error = $"Blocked cell {x},{y} is outside the container bounds.";
                return false;
            }

            Vector2Int cell = new Vector2Int(x, y);
            if (uniqueCells.Add(cell))
            {
                blockedCells.Add(cell);
            }
        }

        return true;
    }

    private static List<string> SplitTsvLine(string line)
    {
        List<string> cells = new List<string>();
        StringBuilder cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char current = line[i];
            if (current == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (current == '\t' && !inQuotes)
            {
                cells.Add(cell.ToString());
                cell.Length = 0;
                continue;
            }

            cell.Append(current);
        }

        cells.Add(cell.ToString());
        return cells;
    }

    private static Dictionary<string, int> BuildHeaderMap(List<string> headers)
    {
        Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Count; i++)
        {
            string header = headers[i].Trim();
            if (!map.ContainsKey(header))
            {
                map.Add(header, i);
            }
        }

        return map;
    }

    private static string GetCell(List<string> cells, Dictionary<string, int> headerIndexes, string header)
    {
        if (!headerIndexes.TryGetValue(header, out int index) || index < 0 || index >= cells.Count)
        {
            return string.Empty;
        }

        return cells[index];
    }

    private static string GetOptionalCell(
        List<string> cells,
        Dictionary<string, int> headerIndexes,
        string header,
        string defaultValue)
    {
        if (!headerIndexes.ContainsKey(header))
        {
            return defaultValue;
        }

        string value = GetCell(cells, headerIndexes, header);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string normalized = assetFolder.Replace("\\", "/");
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static Vector3 ResolveWhiteboxScale(LootItemImportRow row)
    {
        float width = Mathf.Max(0.45f, row.Width * 0.32f);
        float depth = Mathf.Max(0.45f, row.Height * 0.32f);
        float height = row.Type == ItemType.Bag || row.Type == ItemType.Rig ? 0.55f : 0.32f;
        return new Vector3(width, height, depth);
    }

    private static string SanitizeFileName(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value) ? "loot_item" : value.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalidChar, '_');
        }

        return safe;
    }

    private static string ToAssetPath(string absoluteOrAssetPath)
    {
        if (string.IsNullOrWhiteSpace(absoluteOrAssetPath))
        {
            return DefaultTsvPath;
        }

        string normalized = absoluteOrAssetPath.Replace("\\", "/");
        string dataPath = Application.dataPath.Replace("\\", "/");
        if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets" + normalized.Substring(dataPath.Length);
        }

        return normalized;
    }

    private static string ToAbsolutePath(string assetOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(assetOrAbsolutePath))
        {
            return ToAbsolutePath(DefaultTsvPath);
        }

        string normalized = assetOrAbsolutePath.Replace("\\", "/");
        if (Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), normalized));
    }

    private static void FinishReport(LootImportReport report)
    {
        string summary =
            $"Loot TSV Importer: validRows={report.ValidRows}, skippedRows={report.SkippedRows}, " +
            $"createdItems={report.CreatedItems}, updatedItems={report.UpdatedItems}, " +
            $"createdPrefabs={report.CreatedPrefabs}, repairedPrefabs={report.RepairedPrefabs}, " +
            $"errors={report.Errors.Count}, warnings={report.Warnings.Count}";

        if (report.Errors.Count > 0)
        {
            Debug.LogError(summary);
            for (int i = 0; i < report.Errors.Count; i++)
            {
                Debug.LogError(report.Errors[i]);
            }

            EditorUtility.DisplayDialog("Loot TSV Import Failed", summary, "OK");
            return;
        }

        Debug.Log(summary);
        for (int i = 0; i < report.Warnings.Count; i++)
        {
            Debug.LogWarning(report.Warnings[i]);
        }

        for (int i = 0; i < report.Info.Count; i++)
        {
            Debug.Log(report.Info[i]);
        }

        EditorUtility.DisplayDialog("Loot TSV Import", summary, "OK");
    }

    private sealed class LootItemImportRow
    {
        public int RowNumber;
        public string ItemId;
        public string ItemName;
        public string TypeText;
        public string EquipmentKindText;
        public string RarityText;
        public string WidthText;
        public string HeightText;
        public string IsStackableText;
        public string MaxStackText;
        public string ContainerColumnsText;
        public string ContainerRowsText;
        public string BlockedCellsText;
        public string RequiresSearchText;
        public string SearchDurationOverrideText;
        public string MagicUnlockText;
        public string RunePatternPointsText;
        public string CarryWeightText;
        public string SellPriceText;
        public string Notes;
        public ItemType Type;
        public EquipmentSlotKind EquipmentKind;
        public ItemRarity Rarity;
        public int Width;
        public int Height;
        public bool IsStackable;
        public int MaxStack;
        public int ContainerColumns;
        public int ContainerRows;
        public List<Vector2Int> BlockedCells = new List<Vector2Int>();
        public bool RequiresSearchInLootContainer;
        public float SearchDurationOverride;
        public MagicUnlockType MagicUnlock;
        public int RunePatternPoints;
        public float CarryWeight = 1f;
        public int SellPrice;
    }

    private sealed class LootImportReport
    {
        public bool WroteAssets;
        public int ValidRows;
        public int SkippedRows;
        public int CreatedItems;
        public int UpdatedItems;
        public int CreatedPrefabs;
        public int RepairedPrefabs;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Info = new List<string>();
    }
}
