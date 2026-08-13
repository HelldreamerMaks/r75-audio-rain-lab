// R75 Audio-Spawn Digital Rain v12.1
// Single-purpose host command: sound gate controls ONLY creation of NEW rain drops.
// No EEPROM writes, no brightness/speed/color writes, no per-LED host streaming.

#include QMK_KEYBOARD_H
#include "raw_hid.h"

// BUILD SAFETY GATE:
// The audited ISO keyboard.json and ISO SignalRGB plugin both describe 81 RGB LEDs,
// while the fork's shared rk/r75/config.h currently defines 80. Do not silently
// build/flash until that upstream inconsistency is resolved for the ISO target.
#ifndef RGB_MATRIX_LED_COUNT
#    error "RGB_MATRIX_LED_COUNT is not defined for R75 ISO"
#elif RGB_MATRIX_LED_COUNT != 81
#    error "R75 ISO LED-count safety gate: expected 81; resolve shared config.h=80 vs ISO layout=81 before building for flash"
#endif

#define AUDIO_RAIN_GATE_COMMAND 0x29
#define AUDIO_RAIN_MAGIC        0xA7
#define AUDIO_RAIN_PROTOCOL     0x01
#define AUDIO_RAIN_PACKET_SIZE  32

volatile uint8_t  g_audio_rain_spawn_gate    = 0;
volatile uint32_t g_audio_rain_last_packet_ms = 0;

bool via_command_kb(uint8_t *data, uint8_t length) {
    // Preserve every normal VIA command.
    if (data == NULL || length == 0 || data[0] != AUDIO_RAIN_GATE_COMMAND) {
        return false;
    }

    uint8_t response[AUDIO_RAIN_PACKET_SIZE] = {0};
    response[0] = AUDIO_RAIN_GATE_COMMAND;

    if (length != AUDIO_RAIN_PACKET_SIZE) {
        response[1] = 4; // bad length
    } else if (data[1] != AUDIO_RAIN_MAGIC) {
        response[1] = 1; // bad magic
    } else if (data[2] != AUDIO_RAIN_PROTOCOL) {
        response[1] = 2; // bad protocol version
    } else if (data[3] > 1) {
        response[1] = 3; // gate must be exactly 0 or 1
    } else {
        g_audio_rain_spawn_gate     = data[3];
        g_audio_rain_last_packet_ms = timer_read32();

        // Select the custom effect without touching EEPROM, but only once.
        // Re-selecting it on every keepalive could re-initialize the framebuffer.
        if (rgb_matrix_get_mode() != RGB_MATRIX_CUSTOM_AUDIO_DIGITAL_RAIN) {
            rgb_matrix_mode_noeeprom(RGB_MATRIX_CUSTOM_AUDIO_DIGITAL_RAIN);
        }

        response[1] = 0; // success
        response[2] = AUDIO_RAIN_MAGIC;
        response[3] = AUDIO_RAIN_PROTOCOL;
        response[4] = g_audio_rain_spawn_gate;
    }

    raw_hid_send(response, AUDIO_RAIN_PACKET_SIZE);
    return true;
}
