import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const workbookPath = "Assets/Config/Loot/LootItems_Template.xlsx";
const input = await FileBlob.load(workbookPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const table = await workbook.inspect({
  kind: "table",
  range: "LootItems!A1:S30",
  include: "values,formulas",
  tableMaxRows: 30,
  tableMaxCols: 19,
});
console.log(table.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 50 },
  summary: "formula error scan",
});
console.log(errors.ndjson);

await workbook.render({ sheetName: "LootItems", range: "A1:S30", scale: 1 });
console.log("Rendered LootItems!A1:S30 successfully.");
