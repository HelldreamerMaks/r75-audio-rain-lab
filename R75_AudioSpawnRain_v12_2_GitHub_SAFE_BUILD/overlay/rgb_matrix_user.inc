// R75 Audio-Spawn Digital Rain v12.1
// Derived from this fork's stock DIGITAL_RAIN implementation.
// The ONLY behavioral change is `spawn_enabled &&` on NEW-drop creation.
// Existing drops move and decay independently of the sound gate.
//
// !!! DO NOT ADD #pragma once !!!

RGB_MATRIX_EFFECT(AUDIO_DIGITAL_RAIN)

#ifdef RGB_MATRIX_CUSTOM_EFFECT_IMPLS

extern volatile uint8_t  g_audio_rain_spawn_gate;
extern volatile uint32_t g_audio_rain_last_packet_ms;

#ifndef RGB_DIGITAL_RAIN_DROPS
#    define RGB_DIGITAL_RAIN_DROPS 24
#endif

#ifndef AUDIO_RAIN_WATCHDOG_MS
#    define AUDIO_RAIN_WATCHDOG_MS 350
#endif

static bool AUDIO_DIGITAL_RAIN(effect_params_t *params) {
    const uint8_t drop_ticks           = 28;
    const uint8_t max_intensity        = rgb_matrix_config.hsv.v;

    // Stock Digital Rain divides by values derived from brightness.
    // If brightness is 0, render black and keep state intact.
    if (max_intensity == 0) {
        rgb_matrix_set_color_all(0, 0, 0);
        return false;
    }

    const uint8_t pure_green_intensity = (((uint16_t)max_intensity) * 3) >> 2;
    const uint8_t max_brightness_boost = (((uint16_t)max_intensity) * 3) >> 2;
    const uint8_t decay_ticks          = 0xff / max_intensity;

    static uint8_t drop  = 0;
    static uint8_t decay = 0;

    if (params->init) {
        rgb_matrix_set_color_all(0, 0, 0);
        memset(g_rgb_frame_buffer, 0, sizeof(g_rgb_frame_buffer));
        drop = 0;
    }

    // Fail closed: if the host app disappears, only NEW spawning stops.
    // Existing framebuffer contents are intentionally left untouched.
    const bool spawn_enabled =
        (g_audio_rain_spawn_gate != 0) &&
        (timer_elapsed32(g_audio_rain_last_packet_ms) <= AUDIO_RAIN_WATCHDOG_MS);

    decay++;

    for (uint8_t col = 0; col < MATRIX_COLS; col++) {
        for (uint8_t row = 0; row < MATRIX_ROWS; row++) {
            // Single semantic modification vs stock Digital Rain:
            // `spawn_enabled &&` is added only to the NEW-drop condition.
            if (spawn_enabled && row == 0 && drop == 0 && rand() < RAND_MAX / RGB_DIGITAL_RAIN_DROPS) {
                g_rgb_frame_buffer[row][col] = max_intensity;
            } else if (g_rgb_frame_buffer[row][col] > 0 && g_rgb_frame_buffer[row][col] < max_intensity) {
                if (decay == decay_ticks) {
                    g_rgb_frame_buffer[row][col]--;
                }
            }

            uint8_t led[LED_HITS_TO_REMEMBER];
            uint8_t led_count = rgb_matrix_map_row_column_to_led(row, col, led);

            if (led_count > 0) {
                if (g_rgb_frame_buffer[row][col] > pure_green_intensity) {
                    const uint8_t denominator = max_intensity - pure_green_intensity;
                    const uint8_t boost = denominator == 0
                        ? 0
                        : (uint8_t)((uint16_t)max_brightness_boost *
                            (g_rgb_frame_buffer[row][col] - pure_green_intensity) / denominator);
                    rgb_matrix_set_color(led[0], boost, max_intensity, boost);
                } else {
                    const uint8_t green = pure_green_intensity == 0
                        ? 0
                        : (uint8_t)((uint16_t)max_intensity *
                            g_rgb_frame_buffer[row][col] / pure_green_intensity);
                    rgb_matrix_set_color(led[0], 0, green, 0);
                }
            }
        }
    }

    if (decay == decay_ticks) {
        decay = 0;
    }

    // Original movement logic is unchanged and DOES NOT depend on spawn_enabled.
    if (++drop > drop_ticks) {
        drop = 0;

        for (uint8_t row = MATRIX_ROWS - 1; row > 0; row--) {
            for (uint8_t col = 0; col < MATRIX_COLS; col++) {
                if (row == MATRIX_ROWS - 1 && g_rgb_frame_buffer[row][col] == max_intensity) {
                    g_rgb_frame_buffer[row][col]--;
                }

                if (g_rgb_frame_buffer[row - 1][col] >= max_intensity) {
                    g_rgb_frame_buffer[row - 1][col] = max_intensity - 1;
                    g_rgb_frame_buffer[row][col]     = max_intensity;
                }
            }
        }
    }

    return false;
}

#endif // RGB_MATRIX_CUSTOM_EFFECT_IMPLS
