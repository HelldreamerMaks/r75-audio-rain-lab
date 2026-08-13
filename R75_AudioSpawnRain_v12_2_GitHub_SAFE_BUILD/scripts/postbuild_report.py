#!/usr/bin/env python3
from __future__ import annotations
import hashlib, pathlib, shutil, sys
if len(sys.argv) != 4:
    raise SystemExit("usage: postbuild_report.py <qmk_root> <report_dir> <label>")
qmk=pathlib.Path(sys.argv[1]).resolve(); report=pathlib.Path(sys.argv[2]).resolve(); label=sys.argv[3]
report.mkdir(parents=True, exist_ok=True)
patterns=["*.hex","*.bin","*.uf2"]
files=[]
for pat in patterns:
    files.extend(p for p in qmk.glob(pat) if p.is_file())
lines=[f"POSTBUILD REPORT: {label}", "NO FLASH WAS PERFORMED.", ""]
if not files:
    lines.append("No firmware image found at QMK repository root.")
else:
    for p in sorted(files):
        b=p.read_bytes(); sha=hashlib.sha256(b).hexdigest()
        lines += [f"file={p.name}",f"size={len(b)}",f"sha256={sha}",""]
        # Quarantine copy: intentionally NOT a .hex/.bin/.uf2 filename.
        qname=f"QUARANTINE_NOT_FOR_FLASH__{p.name}.disabled"
        shutil.copy2(p, report/qname)
lines.append("All firmware copies in this artifact are renamed with .disabled and are NOT approved for flashing.")
(report/f"{label}_postbuild.txt").write_text("\n".join(lines)+"\n",encoding="utf-8")
print("\n".join(lines))
