#!/usr/bin/env python3
from __future__ import annotations
import pathlib, re, sys
root = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
scan = list((root / ".github/workflows").glob("*.yml"))
scan += [p for p in (root / "scripts").glob("*.py") if p.name != "audit_project_safety.py"]
patterns = {
    "qmk flash": re.compile(r"\bqmk\s+flash\b", re.I),
    "make target flash": re.compile(r"\bmake\b[^\n]*:flash\b", re.I),
    "wb32 download/write": re.compile(r"wb32-dfu-updater_cli[^\n]*(?:\s-D\b|--download\b)", re.I),
    "dfu-util write": re.compile(r"dfu-util[^\n]*(?:\s-D\b|--download\b)", re.I),
}
found=[]
for p in scan:
    text=p.read_text(encoding="utf-8",errors="replace")
    for name,rx in patterns.items():
        if rx.search(text): found.append(f"{p.relative_to(root)}: {name}")
if found:
    print("UNSAFE BUILD PROJECT CONTENT DETECTED")
    print("\n".join(found))
    raise SystemExit(2)
print(f"PASS: scanned {len(scan)} executable workflow/script files; no firmware flashing/write command patterns found.")
