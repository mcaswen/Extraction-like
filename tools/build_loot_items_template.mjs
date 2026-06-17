import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const outputDir = path.resolve("Assets/Config/Loot");
const workbookPath = path.join(outputDir, "LootItems_Template.xlsx");
const tsvPath = path.join(outputDir, "LootItems.tsv");

const headers = [
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
  "CarryWeight",
  "ItemBackgroundSpritePath",
  "IncludeInRuntimeDatabase",
  "IncludeInTotemShop",
  "TotemQuality",
  "TotemModifiers",
  "Notes",
];

const descriptions = [
  "Stable unique key. Use lowercase English letters, numbers, and underscores.",
  "Display name shown to players.",
  "Bag, Rig, Equipment, or Other. Other imports as ItemType.Junk.",
  "Use None for non-equipment; Head, Body, Face, Headphone, or Totem for equipment.",
  "Common, Uncommon, Rare, Epic, or Legendary.",
  "Inventory footprint width. Range 1-10.",
  "Inventory footprint height. Range 1-10.",
  "Use Yes or No.",
  "Maximum stack count. Use 1 when not stackable.",
  "Only fill for Bag or Rig.",
  "Only fill for Bag or Rig.",
  "Only for containers. Format: x,y;x,y",
  "Use Yes or No. Controls whether this item must be searched inside loot containers.",
  "-1 uses the default duration. Otherwise enter non-negative seconds.",
  "MagicUnlockType enum value.",
  "At least 1.",
  "Use Yes or No. No rows are ignored by import.",
  "Non-negative whole number used by future sell-item systems.",
  "Non-negative carry weight. Optional importer column.",
  "Optional Sprite asset path used as the item UI background.",
  "Use Yes or No. No keeps the asset on disk but inactive.",
  "Use Yes or No. No keeps a totem out of the shop pool.",
  "None, Green, Blue, or Gold.",
  "Semicolon list such as MaxHealth=0.1;MoveSpeed=-0.05.",
  "Designer notes. Not imported.",
];

const lootRows = [
  { itemId: "book", itemName: "书", sellPrice: 10 },
  { itemId: "notebook", itemName: "笔记", sellPrice: 20 },
  { itemId: "organic_fiber", itemName: "有机纤维", width: 2, height: 2, sellPrice: 30 },
  { itemId: "military_radio", itemName: "军用对讲机", rarity: "Uncommon", width: 1, height: 2, sellPrice: 40 },
  { itemId: "uphone_phone", itemName: "uphone手机", rarity: "Uncommon", width: 1, height: 2, sellPrice: 60 },
  { itemId: "lesser_organic_fiber", itemName: "次级有机纤维", rarity: "Uncommon", width: 2, height: 2, sellPrice: 80 },
  { itemId: "usb_drive", itemName: "u盘", rarity: "Rare", sellPrice: 80 },
  { itemId: "intel_file", itemName: "情报文件", rarity: "Rare", width: 1, height: 2, sellPrice: 100 },
  { itemId: "advanced_organic_fiber", itemName: "高级有机纤维", rarity: "Rare", width: 2, height: 2, sellPrice: 120 },
  { itemId: "pocket_watch", itemName: "怀表", rarity: "Epic", sellPrice: 400 },
  { itemId: "pure_gold_ring", itemName: "纯金戒指", rarity: "Epic", sellPrice: 300 },
  { itemId: "top_organic_fiber", itemName: "顶级有机纤维", rarity: "Epic", width: 2, height: 3, sellPrice: 500 },
  { itemId: "graphics_card", itemName: "显示卡", rarity: "Legendary", width: 1, height: 2, sellPrice: 1500 },
  { itemId: "saluzzo_aged_wine", itemName: "萨卢佐醇酿", rarity: "Legendary", width: 1, height: 2, sellPrice: 1500 },
  { itemId: "wizard_hat", itemName: "巫师帽", rarity: "Legendary", width: 3, height: 3, sellPrice: 2000 },
  { itemId: "equip_head_green", itemName: "绿色头部装备", type: "Equipment", equipmentKind: "Head", rarity: "Uncommon", width: 2, height: 2, sellPrice: 100 },
  { itemId: "equip_head_blue", itemName: "蓝色头部装备", type: "Equipment", equipmentKind: "Head", rarity: "Rare", width: 2, height: 2, sellPrice: 300 },
  { itemId: "equip_body_green", itemName: "绿色身体装备", type: "Equipment", equipmentKind: "Body", rarity: "Uncommon", width: 2, height: 3, sellPrice: 100 },
  { itemId: "equip_body_blue", itemName: "蓝色身体装备", type: "Equipment", equipmentKind: "Body", rarity: "Rare", width: 2, height: 3, sellPrice: 300 },
  { itemId: "equip_body_gold", itemName: "金色身体装备", type: "Equipment", equipmentKind: "Body", rarity: "Legendary", width: 2, height: 3, sellPrice: 800 },
  { itemId: "equip_face_green", itemName: "绿色面部装备", type: "Equipment", equipmentKind: "Face", rarity: "Uncommon", width: 1, height: 1, sellPrice: 100 },
  { itemId: "equip_face_blue", itemName: "蓝色面部装备", type: "Equipment", equipmentKind: "Face", rarity: "Rare", width: 1, height: 1, sellPrice: 300 },
  { itemId: "equip_headphone_green", itemName: "绿色耳机", type: "Equipment", equipmentKind: "Headphone", rarity: "Uncommon", width: 2, height: 1, sellPrice: 100 },
  { itemId: "equip_headphone_blue", itemName: "蓝色耳机", type: "Equipment", equipmentKind: "Headphone", rarity: "Rare", width: 2, height: 1, sellPrice: 300 },
];

const backgroundSpritePaths = {
  Green: "Assets/Art/Sprites/UI design/Bag/S_ItemIcon_Rarity_Common.png",
  Blue: "Assets/Art/Sprites/UI design/Bag/S_ItemIcon_Rarity_Uncommon.png",
  Gold: "Assets/Art/Sprites/UI design/Bag/S_ItemIcon_Rarity_Epic.png",
};

const qualityConfigs = [
  { suffix: "green", quality: "Green", rarity: "Uncommon", sellPrice: 100 },
  { suffix: "blue", quality: "Blue", rarity: "Rare", sellPrice: 300 },
  { suffix: "gold", quality: "Gold", rarity: "Legendary", sellPrice: 800 },
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

function buildTotemRows() {
  const rows = [];
  for (const group of totemGroups) {
    for (const quality of qualityConfigs) {
      rows.push({
        itemId: `equip_totem_${group.slug}_${quality.suffix}`,
        itemName: group.name,
        type: "Equipment",
        equipmentKind: "Totem",
        rarity: quality.rarity,
        width: 1,
        height: 2,
        sellPrice: quality.sellPrice,
        itemBackgroundSpritePath: backgroundSpritePaths[quality.quality],
        includeInRuntimeDatabase: "Yes",
        includeInTotemShop: "Yes",
        totemQuality: quality.quality,
        totemModifiers: group.modifiers[quality.suffix],
        notes: "Generated from 局外图腾系统.xlsx",
      });
    }
  }

  return rows;
}

const sampleRows = [...lootRows, ...buildTotemRows()].map((row) => [
  row.itemId,
  row.itemName,
  row.type ?? "Other",
  row.equipmentKind ?? "None",
  row.rarity ?? "Common",
  row.width ?? 1,
  row.height ?? 1,
  row.isStackable ?? "No",
  row.maxStack ?? 1,
  row.containerColumns ?? "",
  row.containerRows ?? "",
  row.blockedCells ?? "",
  row.requiresSearch ?? "Yes",
  row.searchDurationOverride ?? -1,
  row.magicUnlock ?? "None",
  row.runePatternPoints ?? 1,
  row.enabled ?? "Yes",
  row.sellPrice ?? 0,
  row.carryWeight ?? 1,
  row.itemBackgroundSpritePath ?? "",
  row.includeInRuntimeDatabase ?? "Yes",
  row.includeInTotemShop ?? "Yes",
  row.totemQuality ?? "",
  row.totemModifiers ?? "",
  row.notes ?? "",
]);

const rarityColors = {
  Common: "#B8E986",
  Uncommon: "#7FB3FF",
  Rare: "#B58CFF",
  Epic: "#FFD76A",
  Legendary: "#FF7A70",
};

function columnName(index) {
  let name = "";
  let current = index + 1;
  while (current > 0) {
    const remainder = (current - 1) % 26;
    name = String.fromCharCode(65 + remainder) + name;
    current = Math.floor((current - 1) / 26);
  }
  return name;
}

function toTsv(rows) {
  return rows
    .map((row) => {
      const cleaned = row.map((value) => String(value ?? "").replace(/\t/g, " ").replace(/\r?\n/g, " "));
      while (cleaned.length > 0 && cleaned[cleaned.length - 1] === "") {
        cleaned.pop();
      }

      return cleaned.join("\t");
    })
    .join("\r\n");
}

function columnRange(column, firstRow, lastRow) {
  return `${column}${firstRow}:${column}${lastRow}`;
}

async function main() {
  await fs.mkdir(outputDir, { recursive: true });

  const workbook = Workbook.create();
  const sheet = workbook.worksheets.add("LootItems");
  const lists = workbook.worksheets.add("Lists");

  const maxRows = 200;
  const lastColumn = columnName(headers.length - 1);
  const values = [headers, descriptions, ...sampleRows];
  sheet.getRange(`A1:${lastColumn}${values.length}`).values = values;

  const fullRange = sheet.getRange(`A1:${lastColumn}${maxRows}`);
  fullRange.format = {
    font: { name: "Calibri", size: 11, color: "#1F2937" },
    verticalAlignment: "center",
    wrapText: true,
  };

  sheet.getRange(`A1:${lastColumn}1`).format = {
    fill: "#1F2937",
    font: { name: "Calibri", size: 11, color: "#FFFFFF", bold: true },
    horizontalAlignment: "center",
    verticalAlignment: "center",
    borders: { preset: "outside", style: "thin", color: "#111827" },
  };

  sheet.getRange(`A2:${lastColumn}2`).format = {
    fill: "#E5E7EB",
    font: { name: "Calibri", size: 10, color: "#374151", italic: true },
    wrapText: true,
    verticalAlignment: "top",
  };

  sheet.getRange(`A3:${lastColumn}${maxRows}`).format.borders = {
    preset: "inside",
    style: "thin",
    color: "#E5E7EB",
  };
  sheet.getRange(`A1:${lastColumn}${maxRows}`).format.autofitColumns();
  sheet.getRange("A:A").format.numberFormat = "@";
  sheet.getRange("B:B").format.numberFormat = "@";
  sheet.getRange("L:L").format.numberFormat = "@";
  sheet.getRange("R:R").format.numberFormat = "0";
  sheet.getRange("S:S").format.numberFormat = "0.##";
  sheet.getRange("T:X").format.numberFormat = "@";
  sheet.freezePanes.freezeRows(2);

  sheet.getRange(columnRange("C", 3, maxRows)).dataValidation = {
    allowBlank: false,
    list: { inCellDropDown: true, source: ["Bag", "Rig", "Equipment", "Other"] },
  };
  sheet.getRange(columnRange("D", 3, maxRows)).dataValidation = {
    allowBlank: false,
    list: { inCellDropDown: true, source: ["None", "Head", "Body", "Face", "Headphone", "Totem"] },
  };
  sheet.getRange(columnRange("E", 3, maxRows)).dataValidation = {
    allowBlank: false,
    list: { inCellDropDown: true, source: ["Common", "Uncommon", "Rare", "Epic", "Legendary"] },
  };
  for (const column of ["H", "M", "Q", "U", "V"]) {
    sheet.getRange(columnRange(column, 3, maxRows)).dataValidation = {
      allowBlank: false,
      list: { inCellDropDown: true, source: ["Yes", "No"] },
    };
  }
  sheet.getRange(columnRange("O", 3, maxRows)).dataValidation = {
    allowBlank: false,
    list: {
      inCellDropDown: true,
      source: [
        "None",
        "IceFreeze",
        "IceCone",
        "EarthWall",
        "RunePattern",
        "TravelerBoots",
        "TimeHourglass",
        "SpaceHourglass",
      ],
    },
  };

  sheet.getRange(columnRange("W", 3, maxRows)).dataValidation = {
    allowBlank: true,
    list: { inCellDropDown: true, source: ["None", "Green", "Blue", "Gold"] },
  };

  for (const column of ["F", "G"]) {
    sheet.getRange(columnRange(column, 3, maxRows)).dataValidation = {
      allowBlank: false,
      rule: { type: "whole", operator: "between", formula1: 1, formula2: 10 },
      errorAlert: {
        style: "stop",
        title: "Invalid footprint",
        message: "Enter a whole number from 1 to 10.",
      },
    };
  }

  for (const column of ["I", "P"]) {
    sheet.getRange(columnRange(column, 3, maxRows)).dataValidation = {
      allowBlank: true,
      rule: { type: "whole", operator: "greaterThanOrEqual", formula1: 1 },
      errorAlert: {
        style: "stop",
        title: "Invalid whole number",
        message: "Enter a whole number of at least 1.",
      },
    };
  }

  sheet.getRange(columnRange("N", 3, maxRows)).dataValidation = {
    allowBlank: false,
    rule: { type: "decimal", operator: "greaterThanOrEqual", formula1: -1 },
    errorAlert: {
      style: "stop",
      title: "Invalid search duration",
      message: "Use -1 for default duration or a non-negative number.",
    },
  };

  sheet.getRange(columnRange("R", 3, maxRows)).dataValidation = {
    allowBlank: false,
    rule: { type: "whole", operator: "greaterThanOrEqual", formula1: 0 },
    errorAlert: {
      style: "stop",
      title: "Invalid sell price",
      message: "Enter a non-negative whole number.",
    },
  };

  sheet.getRange(columnRange("S", 3, maxRows)).dataValidation = {
    allowBlank: false,
    rule: { type: "decimal", operator: "greaterThanOrEqual", formula1: 0 },
    errorAlert: {
      style: "stop",
      title: "Invalid carry weight",
      message: "Enter a non-negative number.",
    },
  };

  const rarityRange = sheet.getRange(`E3:E${maxRows}`);
  for (const [rarity, color] of Object.entries(rarityColors)) {
    rarityRange.conditionalFormats.addCustom(`=$E3="${rarity}"`, {
      fill: color,
      font: { color: "#111827", bold: true },
    });
  }

  lists.getRange("A1:B1").values = [["List", "Value"]];
  lists.getRange("A2:B30").values = [
    ["Type", "Bag"],
    ["Type", "Rig"],
    ["Type", "Equipment"],
    ["Type", "Other"],
    ["EquipmentKind", "None"],
    ["EquipmentKind", "Head"],
    ["EquipmentKind", "Body"],
    ["EquipmentKind", "Face"],
    ["EquipmentKind", "Headphone"],
    ["EquipmentKind", "Totem"],
    ["Rarity", "Common"],
    ["Rarity", "Uncommon"],
    ["Rarity", "Rare"],
    ["Rarity", "Epic"],
    ["Rarity", "Legendary"],
    ["Boolean", "Yes"],
    ["Boolean", "No"],
    ["MagicUnlock", "None"],
    ["MagicUnlock", "IceFreeze"],
    ["MagicUnlock", "IceCone"],
    ["MagicUnlock", "EarthWall"],
    ["MagicUnlock", "RunePattern"],
    ["MagicUnlock", "TravelerBoots"],
    ["MagicUnlock", "TimeHourglass"],
    ["MagicUnlock", "SpaceHourglass"],
    ["TotemQuality", "None"],
    ["TotemQuality", "Green"],
    ["TotemQuality", "Blue"],
    ["TotemQuality", "Gold"],
  ];
  lists.getRange("A1:B30").format.autofitColumns();

  const tsv = `${toTsv([headers, ...sampleRows])}\r\n`;
  await fs.writeFile(tsvPath, tsv, "utf8");

  const output = await SpreadsheetFile.exportXlsx(workbook);
  await output.save(workbookPath);

  console.log(`Wrote ${workbookPath}`);
  console.log(`Wrote ${tsvPath}`);
}

await main();
