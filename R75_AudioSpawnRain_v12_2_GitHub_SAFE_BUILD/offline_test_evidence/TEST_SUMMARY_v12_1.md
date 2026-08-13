# Offline test summary

Executed locally with GCC 14.2, `-Wall -Wextra -Werror`, AddressSanitizer and UndefinedBehaviorSanitizer:

- `audio_rain_handler_harness.c`: PASS
  - non-0x29 commands left for normal VIA processing
  - invalid length/magic/version/gate rejected
  - valid 0/1 ACKed
  - first valid packet switches custom mode using no-EEPROM API
  - keepalive does not reinitialize framebuffer

- `qmk_effect_harness.c`: PASS
  - gate OFF from empty: 20,000 frames, zero new drops
  - gate ON: drops spawn
  - ON -> OFF: no new top-row drops; existing state drains naturally
  - stale-host watchdog fails closed
  - brightness 0 guarded from division-by-zero

- `routing_model.py`: PASS
  - naked 0x29 is swallowed while default OpenRGB routing is active
  - valid SignalRGB 0x22 changes routing; subsequent 0x29 reaches `via_command_kb`

- `model_verify.py`: PASS
  - stock-vs-custom gate-ON parity: 10,000 model frames
  - gate OFF: 20,000 model frames, zero drops
  - existing drops drain after ON -> OFF
  - host order VIA -> SignalRGB 1.0.5 -> private 0x29
  - single-purpose audit

## New pre-build result

`LED_COUNT_AUDIT.txt`: **BLOCKER**
- shared macro = 80
- ISO RGB layout = 81
- ANSI RGB layout = 80
- ISO E483 plugin = 81 indices (0..80)

The firmware overlay contains a compile-time guard to fail closed until the ISO count is resolved.

Limit: this environment does not contain the WB32/QMK cross-toolchain or .NET SDK. These are not full target-build results and no `.hex` is claimed.

- `LED_COUNT_COMPILE_GATE.txt`: PASS
  - synthetic audited count 81: handler harness compiles/runs
  - upstream shared count 80: compilation is intentionally stopped by the v12.1 safety gate
