import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "C:/Users/VICTOR/Desktop/Sistema de Riego/Planificacion Victor Madrid Final.xlsx";
const outDir = "C:/Users/VICTOR/Desktop/Sistema de Riego/qa_planificacion";
await fs.mkdir(outDir, { recursive: true });

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const summary = await workbook.inspect({
  kind: "workbook,sheet,table,region",
  maxChars: 30000,
  tableMaxRows: 60,
  tableMaxCols: 20,
  tableMaxCellChars: 250,
});
console.log("=== SUMMARY ===");
console.log(summary.ndjson);

const sheets = workbook.worksheets.items;
for (const sheet of sheets) {
  const used = sheet.getUsedRange();
  console.log(`=== SHEET: ${sheet.name} ===`);
  if (used) {
    const details = await workbook.inspect({
      kind: "table",
      sheetId: sheet.name,
      range: used.address?.split("!").pop(),
      include: "values,formulas",
      maxChars: 30000,
      tableMaxRows: 120,
      tableMaxCols: 30,
      tableMaxCellChars: 300,
    });
    console.log(details.ndjson);
  }
  const preview = await workbook.render({ sheetName: sheet.name, autoCrop: "all", scale: 1.5, format: "png" });
  const safe = sheet.name.replace(/[\\/:*?"<>|]/g, "_");
  await fs.writeFile(`${outDir}/${safe}.png`, new Uint8Array(await preview.arrayBuffer()));
}
