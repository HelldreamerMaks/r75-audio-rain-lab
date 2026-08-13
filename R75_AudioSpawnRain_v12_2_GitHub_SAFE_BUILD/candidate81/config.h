// BUILD-ANALYSIS ONLY for RK R75 ISO E483.
// This override is applied only inside a GitHub Actions runner.
// It does NOT touch a physical keyboard and is NOT approval to flash the result.
#pragma once

#undef RGB_MATRIX_LED_COUNT
#define RGB_MATRIX_LED_COUNT 81
