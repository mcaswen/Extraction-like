import crypto from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";

const itemDataScriptGuid = "2a7c8e8af64c58443a9af35d041f773d";
const itemDatabaseScriptGuid = "d11c4dfb2d8d4a64daf7f27fbce96a6b";
const totemShopPoolScriptGuid = "4477c7797defc1f4f9dab4680a3114b1";

const itemDataDir = "Assets/SO/ItemData/Table";
const worldPrefabDir = "Assets/Prefabs/ItemPrefabIn3D";
const itemDatabasePath = "Assets/Resources/Inventory/InventoryItemDatabase.asset";
const shopPoolPath = "Assets/Resources/Shop/TotemShopPool.asset";
const lootTsvPath = "Assets/Config/Loot/LootItems.tsv";

const oldTotemIds = new Set([
  "equip_totem_green",
  "equip_totem_blue",
  "equip_totem_gold",
]);

const backgroundSprites = {
  Green: {
    path: "Assets/Art/Sprites/UI design/Bag/S_ItemIcon_Rarity_Common.png",
    guid: "a4f2db1f9f5f47dca96be7d8af6f0c11",
  },
  Blue: {
    path: "Assets/Art/Sprites/UI design/Bag/S_ItemIcon_Rarity_Uncommon.png",
    guid: "c48a245dd5c94721b8a25cc0f6c2ed31",
  },
  Gold: {
    path: "Assets/Art/Sprites/UI design/Bag/S_ItemIcon_Rarity_Epic.png",
    guid: "e77dcbb66184456db816d3eb99bf49a2",
  },
};

const iconSprites = {
  life: {
    path: "Assets/Art/Sprites/Png_Item_totem/Png_Item_life.PNG",
    guid: "b9e60f174483e4c89857b22cb8fd4e01",
  },
  sniper: {
    path: "Assets/Art/Sprites/Png_Item_totem/Png_Item_juji.PNG",
    guid: "b8b0389e4ebc541598b67970c9530102",
  },
  frost: {
    path: "Assets/Art/Sprites/Png_Item_totem/Png_Item_bingshuang.PNG",
    guid: "3e9f13d7437614afeb375fd6cca060ae",
  },
  earth: {
    path: "Assets/Art/Sprites/Png_Item_totem/Png_Item_diming.PNG",
    guid: "36e456f2276d342c3b12ec08b0174cd5",
  },
  assault: {
    path: "Assets/Art/Sprites/Png_Item_totem/Png_Item_jinji.PNG",
    guid: "f105ab825c4e64a998e082049cac74bb",
  },
  lightness: {
    path: "Assets/Art/Sprites/Png_Item_totem/Png_Item_qingying.PNG",
    guid: "252a275646e634eda88a80d11edbc007",
  },
};

const qualityConfigs = [
  {
    suffix: "green",
    quality: "Green",
    qualityValue: 1,
    rarity: "Uncommon",
    rarityValue: 1,
    sellPrice: 100,
    sourcePrefab: "Assets/Prefabs/ItemPrefabIn3D/World_equip_totem_green.prefab",
  },
  {
    suffix: "blue",
    quality: "Blue",
    qualityValue: 2,
    rarity: "Rare",
    rarityValue: 2,
    sellPrice: 300,
    sourcePrefab: "Assets/Prefabs/ItemPrefabIn3D/World_equip_totem_blue.prefab",
  },
  {
    suffix: "gold",
    quality: "Gold",
    qualityValue: 3,
    rarity: "Legendary",
    rarityValue: 4,
    sellPrice: 800,
    sourcePrefab: "Assets/Prefabs/ItemPrefabIn3D/World_equip_totem_gold.prefab",
  },
];

const totemGroups = [
  {
    slug: "life",
    name: "生命图腾",
    modifiers: {
      green: "MaxHealth=0.1",
      blue: "MaxHealth=0.2",
      gold: "MaxHealth=0.3;MoveSpeed=-0.05",
    },
  },
  {
    slug: "sniper",
    name: "狙击图腾",
    modifiers: {
      green: "AttackRange=0.1;TargetDiscoveryRange=0.1",
      blue: "AttackRange=0.15;TargetDiscoveryRange=0.15",
      gold: "AttackRange=0.3;TargetDiscoveryRange=0.3",
    },
  },
  {
    slug: "frost",
    name: "冰霜图腾",
    modifiers: {
      green: "IceSkillDamage=0.1",
      blue: "IceSkillDamage=0.2",
      gold: "IceSkillDamage=0.3",
    },
  },
  {
    slug: "earth",
    name: "地鸣图腾",
    modifiers: {
      green: "EarthSkillDamage=0.1",
      blue: "EarthSkillDamage=0.2",
      gold: "EarthSkillDamage=0.3",
    },
  },
  {
    slug: "assault",
    name: "进击图腾",
    modifiers: {
      green: "NormalAttackDamage=0.1",
      blue: "NormalAttackDamage=0.2",
      gold: "NormalAttackDamage=0.4;AttackRange=-0.1",
    },
  },
  {
    slug: "lightness",
    name: "轻盈图腾",
    modifiers: {
      green: "MoveSpeed=0.15",
      blue: "MoveSpeed=0.25",
      gold: "MoveSpeed=0.4",
    },
  },
];

const modifierEnumValues = {
  MaxHealth: 0,
  MoveSpeed: 1,
  AttackRange: 2,
  TargetDiscoveryRange: 3,
  NormalAttackDamage: 4,
  IceSkillDamage: 5,
  EarthSkillDamage: 6,
};

function guidFor(kind, value) {
  return crypto.createHash("md5").update(`${kind}:${value}`).digest("hex");
}

function yamlString(value) {
  return JSON.stringify(value ?? "");
}

function parseMetaGuid(metaText) {
  const match = metaText.match(/^guid:\s*([0-9a-f]{32})/m);
  return match ? match[1] : "";
}

async function readTextIfExists(filePath) {
  try {
    return await fs.readFile(filePath, "utf8");
  } catch (error) {
    if (error && error.code === "ENOENT") {
      return "";
    }
    throw error;
  }
}

async function writeMeta(filePath, guid, importer) {
  const metaPath = `${filePath}.meta`;
  const existing = await readTextIfExists(metaPath);
  const finalGuid = parseMetaGuid(existing) || guid;
  await fs.writeFile(metaPath, `fileFormatVersion: 2
guid: ${finalGuid}
${importer}
`, "utf8");
  return finalGuid;
}

function parsePrefabRootFileId(prefabText) {
  const match = prefabText.match(/^--- !u!1 &(-?\d+)/m);
  if (!match) {
    throw new Error("Could not find prefab root fileID.");
  }

  return match[1];
}

function parseWorldItemDataGuid(prefabText) {
  const match = prefabText.match(/ItemData:\s*\{fileID:\s*11400000,\s*guid:\s*([0-9a-f]{32}),\s*type:\s*2\}/);
  if (!match) {
    throw new Error("Could not find WorldLootItem.ItemData guid.");
  }

  return match[1];
}

function parseTotemModifiers(text) {
  if (!text) {
    return [];
  }

  return text.split(";").filter(Boolean).map((entry) => {
    const [type, rawPercent] = entry.split("=");
    if (!(type in modifierEnumValues)) {
      throw new Error(`Unknown modifier type: ${type}`);
    }

    return {
      type,
      typeValue: modifierEnumValues[type],
      percent: Number(rawPercent),
    };
  });
}

function buildItemAsset(row, itemGuid, worldPrefabGuid, worldPrefabFileId) {
  const background = backgroundSprites[row.quality];
  const icon = iconSprites[row.iconKey];
  const modifiers = parseTotemModifiers(row.modifiers);
  const modifierLines = modifiers.length === 0
    ? "  TotemModifiers: []"
    : `  TotemModifiers:
${modifiers.map((modifier) => `  - Type: ${modifier.typeValue}
    Percent: ${modifier.percent}`).join("\n")}`;

  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${itemDataScriptGuid}, type: 3}
  m_Name: ${row.itemId}
  m_EditorClassIdentifier: 
  ContainerColumns: 0
  ContainerRows: 0
  BlockedCells: []
  ItemID: ${row.itemId}
  ItemName: ${yamlString(row.itemName)}
  ItemIcon: {fileID: 21300000, guid: ${icon.guid}, type: 3}
  ItemBackgroundSprite: {fileID: 21300000, guid: ${background.guid}, type: 3}
  IncludeInRuntimeDatabase: 1
  IncludeInTotemShop: 1
  Type: 6
  Rarity: ${row.rarityValue}
  EquipmentKind: 5
  Width: 1
  Height: 2
  IsStackable: 0
  MaxStack: 1
  WorldPrefab: {fileID: ${worldPrefabFileId}, guid: ${worldPrefabGuid}, type: 3}
  SellPrice: ${row.sellPrice}
  CarryWeight: 1
  RequiresSearchInLootContainer: 1
  SearchDurationOverride: -1
  MagicUnlock: 0
  RunePatternPoints: 1
  TotemQuality: ${row.qualityValue}
${modifierLines}
`;
}

function buildTsvRow(row, headers) {
  const values = {
    ItemID: row.itemId,
    ItemName: row.itemName,
    Type: "Equipment",
    EquipmentKind: "Totem",
    Rarity: row.rarity,
    Width: 1,
    Height: 2,
    IsStackable: "No",
    MaxStack: 1,
    ContainerColumns: "",
    ContainerRows: "",
    BlockedCells: "",
    RequiresSearchInLootContainer: "Yes",
    SearchDurationOverride: -1,
    MagicUnlock: "None",
    RunePatternPoints: 1,
    Enabled: "Yes",
    SellPrice: row.sellPrice,
    CarryWeight: 1,
    ItemBackgroundSpritePath: backgroundSprites[row.quality].path,
    IncludeInRuntimeDatabase: "Yes",
    IncludeInTotemShop: "Yes",
    TotemQuality: row.quality,
    TotemModifiers: row.modifiers,
    Notes: "Generated from 局外图腾系统.xlsx",
  };

  return headers.map((header) => String(values[header] ?? ""));
}

function normalizeTsvCell(value) {
  return String(value ?? "").replace(/\t/g, " ").replace(/\r?\n/g, " ").trim();
}

function toTsv(rows) {
  return rows.map((row) => {
    const cleaned = row.map(normalizeTsvCell);
    while (cleaned.length > 0 && cleaned[cleaned.length - 1] === "") {
      cleaned.pop();
    }

    return cleaned.join("\t");
  }).join("\r\n") + "\r\n";
}

async function updateLootTsv(totemRows) {
  const optionalHeaders = [
    "CarryWeight",
    "ItemBackgroundSpritePath",
    "IncludeInRuntimeDatabase",
    "IncludeInTotemShop",
    "TotemQuality",
    "TotemModifiers",
  ];
  const tsvText = await readTextIfExists(lootTsvPath);
  const lines = tsvText.split(/\r?\n/).filter((line) => line.length > 0);
  const headers = lines.length > 0 ? lines[0].split("\t").map((header) => header.replace(/^\uFEFF/, "")) : [];
  const noteIndex = headers.indexOf("Notes");
  for (const header of optionalHeaders) {
    if (headers.includes(header)) {
      continue;
    }

    if (noteIndex >= 0) {
      headers.splice(headers.indexOf("Notes"), 0, header);
    } else {
      headers.push(header);
    }
  }

  const itemIdIndex = headers.indexOf("ItemID");
  const existingRows = [];
  for (let i = 1; i < lines.length; i++) {
    const cells = lines[i].split("\t");
    const itemId = itemIdIndex >= 0 ? (cells[itemIdIndex] ?? "").trim() : "";
    if (oldTotemIds.has(itemId) || totemRows.some((row) => row.itemId === itemId)) {
      continue;
    }

    while (cells.length < headers.length) {
      cells.push("");
    }

    setDefault(cells, headers, "CarryWeight", "1");
    setDefault(cells, headers, "IncludeInRuntimeDatabase", "Yes");
    setDefault(cells, headers, "IncludeInTotemShop", "Yes");
    existingRows.push(cells.slice(0, headers.length));
  }

  const generatedRows = totemRows.map((row) => buildTsvRow(row, headers));
  await fs.writeFile(lootTsvPath, toTsv([headers, ...existingRows, ...generatedRows]), "utf8");
}

function setDefault(cells, headers, header, value) {
  const index = headers.indexOf(header);
  if (index >= 0 && !cells[index]) {
    cells[index] = value;
  }
}

async function patchOldTotemAsset(assetPath) {
  let text = await readTextIfExists(assetPath);
  if (!text) {
    return;
  }

  text = setYamlScalar(text, "IncludeInRuntimeDatabase", "0", "ItemIcon:");
  text = setYamlScalar(text, "IncludeInTotemShop", "0", "IncludeInRuntimeDatabase:");
  text = setYamlScalar(text, "ItemBackgroundSprite", "{fileID: 0}", "ItemIcon:");
  text = setYamlScalar(text, "TotemQuality", "0", "RunePatternPoints:");
  text = setYamlScalar(text, "TotemModifiers", "[]", "TotemQuality:");
  await fs.writeFile(assetPath, text, "utf8");
}

function setYamlScalar(text, fieldName, value, afterFieldPrefix) {
  const lineRegex = new RegExp(`^  ${fieldName}:.*$`, "m");
  if (lineRegex.test(text)) {
    return text.replace(lineRegex, `  ${fieldName}: ${value}`);
  }

  const lines = text.split(/\r?\n/);
  const insertIndex = lines.findIndex((line) => line.trimStart().startsWith(afterFieldPrefix));
  if (insertIndex < 0) {
    lines.push(`  ${fieldName}: ${value}`);
  } else {
    lines.splice(insertIndex + 1, 0, `  ${fieldName}: ${value}`);
  }

  return lines.join("\n");
}

async function generateAssets(totemRows) {
  await fs.mkdir(itemDataDir, { recursive: true });
  await fs.mkdir(worldPrefabDir, { recursive: true });

  const prefabCache = new Map();
  for (const quality of qualityConfigs) {
    const prefabText = await fs.readFile(quality.sourcePrefab, "utf8");
    prefabCache.set(quality.suffix, {
      text: prefabText,
      rootFileId: parsePrefabRootFileId(prefabText),
      oldItemGuid: parseWorldItemDataGuid(prefabText),
    });
  }

  for (const row of totemRows) {
    const itemPath = `${itemDataDir}/${row.itemId}.asset`;
    const itemGuid = await writeMeta(itemPath, guidFor("totem-item", row.itemId), `NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: `);

    const prefabPath = `${worldPrefabDir}/World_${row.itemId}.prefab`;
    const prefabGuid = await writeMeta(prefabPath, guidFor("totem-world", row.itemId), `PrefabImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: `);
    const prefabSource = prefabCache.get(row.qualitySuffix);
    const prefabText = prefabSource.text
      .replace(new RegExp(`World_equip_totem_${row.qualitySuffix}`, "g"), `World_${row.itemId}`)
      .replace(new RegExp(prefabSource.oldItemGuid, "g"), itemGuid);
    await fs.writeFile(prefabPath, prefabText, "utf8");

    const itemAssetText = buildItemAsset(row, itemGuid, prefabGuid, prefabSource.rootFileId);
    await fs.writeFile(itemPath, itemAssetText, "utf8");
  }

  for (const id of oldTotemIds) {
    await patchOldTotemAsset(`${itemDataDir}/${id}.asset`);
  }
}

async function collectInventoryItemAssets(rootDir) {
  const result = [];
  async function walk(dir) {
    const entries = await fs.readdir(dir, { withFileTypes: true });
    for (const entry of entries) {
      const fullPath = path.join(dir, entry.name).replace(/\\/g, "/");
      if (entry.isDirectory()) {
        await walk(fullPath);
      } else if (entry.isFile() && entry.name.endsWith(".asset")) {
        const text = await readTextIfExists(fullPath);
        if (!text.includes(`guid: ${itemDataScriptGuid}`)) {
          continue;
        }

        const itemId = matchScalar(text, "ItemID") || path.basename(fullPath, ".asset");
        const include = matchScalar(text, "IncludeInRuntimeDatabase");
        if (oldTotemIds.has(itemId) || include === "0") {
          continue;
        }

        const meta = await readTextIfExists(`${fullPath}.meta`);
        const guid = parseMetaGuid(meta);
        if (!guid) {
          continue;
        }

        result.push({
          path: fullPath,
          guid,
          itemId,
          equipmentKind: Number(matchScalar(text, "EquipmentKind") || 0),
          sellPrice: Number(matchScalar(text, "SellPrice") || 0),
          includeShop: matchScalar(text, "IncludeInTotemShop") !== "0",
        });
      }
    }
  }

  await walk(rootDir);
  result.sort((left, right) => left.path.localeCompare(right.path));
  return result;
}

function matchScalar(text, fieldName) {
  const match = text.match(new RegExp(`^  ${fieldName}:\\s*(.*)$`, "m"));
  if (!match) {
    return "";
  }

  return match[1].replace(/^"|"$/g, "").trim();
}

async function refreshDatabaseAndShopPool() {
  await fs.mkdir(path.dirname(itemDatabasePath), { recursive: true });
  await fs.mkdir(path.dirname(shopPoolPath), { recursive: true });
  const items = await collectInventoryItemAssets("Assets/SO/ItemData");

  await writeMeta(itemDatabasePath, guidFor("inventory-db", "default"), `NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: `);
  const databaseText = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${itemDatabaseScriptGuid}, type: 3}
  m_Name: InventoryItemDatabase
  m_EditorClassIdentifier: 
  Items:
${items.map((item) => `  - {fileID: 11400000, guid: ${item.guid}, type: 2}`).join("\n")}
`;
  await fs.writeFile(itemDatabasePath, databaseText, "utf8");

  await writeMeta(shopPoolPath, guidFor("totem-shop-pool", "default"), `NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: `);
  const shopItems = items.filter((item) => item.equipmentKind === 5 && item.includeShop);
  const shopPoolText = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${totemShopPoolScriptGuid}, type: 3}
  m_Name: TotemShopPool
  m_EditorClassIdentifier: 
  _entries:
${shopItems.map((item) => `  - ItemData: {fileID: 11400000, guid: ${item.guid}, type: 2}
    BuyPrice: ${Math.max(1, item.sellPrice * 2)}
    Weight: 1`).join("\n")}
`;
  await fs.writeFile(shopPoolPath, shopPoolText, "utf8");
}

function buildTotemRows() {
  const rows = [];
  for (const group of totemGroups) {
    for (const quality of qualityConfigs) {
      rows.push({
        itemId: `equip_totem_${group.slug}_${quality.suffix}`,
        itemName: group.name,
        qualitySuffix: quality.suffix,
        quality: quality.quality,
        qualityValue: quality.qualityValue,
        iconKey: group.slug,
        rarity: quality.rarity,
        rarityValue: quality.rarityValue,
        sellPrice: quality.sellPrice,
        modifiers: group.modifiers[quality.suffix],
      });
    }
  }

  return rows;
}

const totemRows = buildTotemRows();
await updateLootTsv(totemRows);
await generateAssets(totemRows);
await refreshDatabaseAndShopPool();
console.log(`Generated ${totemRows.length} active totems.`);
