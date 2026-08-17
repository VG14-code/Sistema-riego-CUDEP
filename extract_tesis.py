from pathlib import Path
import json
import pdfplumber
from pypdf import PdfReader

root = Path(r"C:\Users\VICTOR\Desktop\Sistema de Riego")
pdf_path = root / "VictorGabriel_MadridBarrios_1690_22_4772.pdf"
out_dir = root / "tmp" / "pdfs"
out_dir.mkdir(parents=True, exist_ok=True)

reader = PdfReader(str(pdf_path))
metadata = {
    "pages": len(reader.pages),
    "metadata": {str(k): str(v) for k, v in (reader.metadata or {}).items()},
}
(out_dir / "metadata.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")

parts = []
with pdfplumber.open(str(pdf_path)) as pdf:
    for number, page in enumerate(pdf.pages, start=1):
        text = page.extract_text(x_tolerance=2, y_tolerance=3) or ""
        parts.append(f"\n\n===== PÁGINA {number} =====\n{text}")

(out_dir / "tesis_texto.txt").write_text("".join(parts), encoding="utf-8")
print(json.dumps(metadata, ensure_ascii=False))
