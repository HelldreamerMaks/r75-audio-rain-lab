# Exact source facts checked before packaging

Repository: snakkarike/qmk_firmware
Branch: OpenSignalRGB-2026
Target: rk/r75/iso:via

ISO keyboard.json:
- processor WB32FQ95
- bootloader wb32-dfu
- USB 342D:E483
- RGB Matrix driver ws2812
- raw feature enabled
- 81 rgb_matrix.layout entries

Shared rk/r75/config.h:
- RGB_MATRIX_LED_COUNT 80 (known blocker)
- RGB_MATRIX_FRAMEBUFFER_EFFECTS enabled
- ENABLE_RGB_MATRIX_DIGITAL_RAIN enabled

ISO VIA rules.mk:
- VIA_ENABLE yes
- OPENRGB_ENABLE yes
- RAW_ENABLE yes
- SIGNALRGB_SUPPORT_ENABLE yes

Fork SignalRGB:
- commands 0x21..0x28
- protocol 1.0.5
- firmware type 2

Fork VIA raw HID routing:
- 0x22 is one of commands that sets SignalRGB routing active and OpenRGB inactive
- once OpenRGB is inactive, command 0x29 falls through to via_command_kb()

Fork Digital Rain:
- new drop condition is top row + drop timer + random test
- movement to lower rows is a separate loop
