#!/usr/bin/env python3
from __future__ import annotations
import pathlib, shutil, sys
if len(sys.argv) != 3:
    raise SystemExit("usage: apply_overlay.py <project_root> <qmk_root>")
project=pathlib.Path(sys.argv[1]).resolve(); qmk=pathlib.Path(sys.argv[2]).resolve()
target=qmk/"keyboards/rk/r75/iso/keymaps/via"
if not target.is_dir(): raise SystemExit(f"missing target {target}")
# Preserve exact upstream rules and only append two unique build lines.
for name in ["audio_rain.c","rgb_matrix_user.inc"]:
    shutil.copy2(project/"overlay"/name,target/name)
rules=target/"rules.mk"; text=rules.read_text(encoding="utf-8")
for line in ["RGB_MATRIX_CUSTOM_USER = yes","SRC += audio_rain.c"]:
    if line not in text:
        text += ("" if text.endswith("\n") else "\n") + line + "\n"
rules.write_text(text,encoding="utf-8")
# Keymap-local candidate override: cloud build analysis ONLY.
shutil.copy2(project/"candidate81/config.h", target/"config.h")
print("Applied keymap-only Audio Rain overlay and candidate81 config override.")
