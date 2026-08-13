#!/usr/bin/env python3
from __future__ import annotations
import json, pathlib, re, sys

if len(sys.argv) != 3:
    raise SystemExit("usage: audit_source.py <qmk_root> <report_path>")
root = pathlib.Path(sys.argv[1]).resolve()
report_path = pathlib.Path(sys.argv[2]).resolve()

iso_json = root / "keyboards/rk/r75/iso/keyboard.json"
ansi_json = root / "keyboards/rk/r75/ansi/keyboard.json"
shared_cfg = root / "keyboards/rk/r75/config.h"
via_rules = root / "keyboards/rk/r75/iso/keymaps/via/rules.mk"
srgb_h = root / "quantum/signalrgb.h"
via_c = root / "quantum/via.c"
rain_h = root / "quantum/rgb_matrix/animations/digital_rain_anim.h"

for p in [iso_json, ansi_json, shared_cfg, via_rules, srgb_h, via_c, rain_h]:
    if not p.is_file():
        raise SystemExit(f"MISSING REQUIRED SOURCE: {p}")

iso = json.loads(iso_json.read_text(encoding="utf-8"))
ansi = json.loads(ansi_json.read_text(encoding="utf-8"))
cfg = shared_cfg.read_text(encoding="utf-8", errors="replace")
rules = via_rules.read_text(encoding="utf-8", errors="replace")
srgb = srgb_h.read_text(encoding="utf-8", errors="replace")
via = via_c.read_text(encoding="utf-8", errors="replace")
rain = rain_h.read_text(encoding="utf-8", errors="replace")

checks: list[tuple[str,bool,str]] = []
def check(name: str, cond: bool, detail: str):
    checks.append((name, bool(cond), detail))

check("ISO keyboard_name", iso.get("keyboard_name") == "Royal Kludge R75 ISO", repr(iso.get("keyboard_name")))
check("ISO processor", iso.get("processor") == "WB32FQ95", repr(iso.get("processor")))
check("ISO bootloader", iso.get("bootloader") == "wb32-dfu", repr(iso.get("bootloader")))
usb = iso.get("usb", {})
check("ISO VID", str(usb.get("vid", "")).lower() == "0x342d", repr(usb.get("vid")))
check("ISO PID", str(usb.get("pid", "")).lower() == "0xe483", repr(usb.get("pid")))
iso_leds = len(iso.get("rgb_matrix", {}).get("layout", []))
ansi_leds = len(ansi.get("rgb_matrix", {}).get("layout", []))
check("ISO rgb layout entries", iso_leds == 81, str(iso_leds))
check("ANSI rgb layout entries", ansi_leds == 80, str(ansi_leds))

m = re.search(r"^\s*#\s*define\s+RGB_MATRIX_LED_COUNT\s+(\d+)\s*$", cfg, re.M)
shared_led_count = int(m.group(1)) if m else None
check("shared config LED count is known blocker 80", shared_led_count == 80, repr(shared_led_count))

for flag in ["VIA_ENABLE = yes", "OPENRGB_ENABLE = yes", "RAW_ENABLE = yes", "SIGNALRGB_SUPPORT_ENABLE = yes"]:
    check(f"rules: {flag}", flag in rules, flag)

ver = []
for key in ["PROTOCOL_VERSION_BYTE_1", "PROTOCOL_VERSION_BYTE_2", "PROTOCOL_VERSION_BYTE_3"]:
    mm = re.search(rf"\b{key}\s*=\s*(\d+)", srgb)
    ver.append(int(mm.group(1)) if mm else None)
check("SignalRGB protocol 1.0.5", ver == [1,0,5], ".".join(map(str, ver)))

check("SignalRGB range remains 0x21..0x28", "GET_QMK_VERSION = 0x21" in srgb and "GET_FIRMWARE_TYPE = 0x28" in srgb, "enum checked")
check("via router recognizes SignalRGB 0x22", "data[0] == 0x22" in via, "0x22 auto-switch")
check("via router has weak via_command_kb", "__attribute__((weak)) bool via_command_kb" in via, "hook exists")
check("via router calls via_command_kb", "if (via_command_kb(data, length))" in via, "hook invoked")
check("OpenRGB routing can be disabled by SignalRGB init", "is_orgb_mode = 0" in via and "is_srgb_mode = 1" in via, "routing switch found")
check("stock Digital Rain spawn trigger found", "row == 0 && drop == 0 && rand()" in rain, "spawn condition")
check("stock Digital Rain movement independent block found", "g_rgb_frame_buffer[row - 1][col] >= max_intensity" in rain, "movement condition")

lines = [
    "R75 SOURCE AUDIT — NO HARDWARE / NO FLASH",
    f"QMK root: {root}",
    "",
]
for name, ok, detail in checks:
    lines.append(f"{'PASS' if ok else 'FAIL'} | {name} | {detail}")
lines += [
    "",
    f"Observed ISO RGB entries: {iso_leds}",
    f"Observed ANSI RGB entries: {ansi_leds}",
    f"Observed shared RGB_MATRIX_LED_COUNT: {shared_led_count}",
    "",
    "IMPORTANT: 80 vs 81 is intentionally NOT silently reconciled by this audit.",
    "Candidate81 build is a separate cloud-only experiment and remains NOT FOR FLASH.",
]
report_path.parent.mkdir(parents=True, exist_ok=True)
report_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(report_path.read_text(encoding="utf-8"))

failed = [name for name, ok, _ in checks if not ok]
if failed:
    raise SystemExit("AUDIT FAILED: " + "; ".join(failed))
