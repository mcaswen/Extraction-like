import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const workbookPath = "Assets/Config/Loot/LootItems_Template.xlsx";
const tsvPath = "Assets/Config/Loot/LootItems.tsv";
const sheetName = "LootItems";
const dataStartRow = 3;
const maxRows = 200;

const rarityColors = {
  Common: "#B8E986",
  Uncommon: "#7FB3FF",
  Rare: "#B58CFF",
  Epic: "#FFD76A",
  Legendary: "#FF7A70",
};

function cleanCell(value) {
  if (value === null || value === undefined) {
    return "";
  }

  return String(value).replace(/\t/g, " ").replace(/\r?\n/g, " ").trim();
}

function toTsv(rows) {
  return rows
    .map((row) => row.map((value) => cleanCell(value)).join("\t"))
    .join("\r\n");
}

const input = await FileBlob.load(workbookPath);
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem(sheetName);

const rarityRange = sheet.getRange(`D${dataStartRow}:D${maxRows}`);
rarityRange.conditionalFormats.deleteAll();
for (const [rarity, color] of Object.entries(rarityColors)) {
  rarityRange.conditionalFormats.addCustom(`=$D${dataStartRow}="${rarity}"`, {
    fill: color,
    font: { color: "#111827", bold: true },
  });
}

for (const column of ["I", "J", "K"]) {
  const range = sheet.getRange(`${column}${dataStartRow}:${column}${maxRows}`);
  range.clear({ applyTo: "contents" });
  range.dataValidation = null;
}

const rawValues = sheet.getRange(`A1:R${maxRows}`).values;
const headers = rawValues[0].map(cleanCell);
const dataRows = rawValues
  .slice(dataStartRow - 1)
  .filter((row) => row.some((value) => cleanCell(value) !== ""))
  .map((row) => {
    const normalized = row.slice(0, headers.length);
    while (normalized.length < headers.length) {
      normalized.push("");
    }

    if (cleanCell(normalized[2]).toLowerCase() === "other") {
      normalized[8] = "";
      normalized[9] = "";
      normalized[10] = "";
    }

    return normalized;
  });

await fs.writeFile(tsvPath, `${toTsv([headers, ...dataRows])}\r\n`, "utf8");

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(workbookPath);

console.log(`Updated rarity conditional formatting in ${workbookPath}`);
console.log(`Exported ${dataRows.length} data rows to ${tsvPath}`);
