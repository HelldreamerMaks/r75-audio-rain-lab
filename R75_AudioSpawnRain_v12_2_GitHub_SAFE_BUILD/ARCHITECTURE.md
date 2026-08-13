# Audio Spawn Rain — одна функция

Host:

`WASAPI RMS -> sound present 0/1 -> keepalive Raw HID 0x29`

Firmware:

`0x29 gate -> spawn_enabled`

Digital Rain:

`spawn_enabled` участвует ТОЛЬКО в условии рождения новой капли.
Movement/decay старых капель не зависит от host/audio.

## Safety semantics

- Первое private состояние: SPAWN OFF.
- Watchdog: если валидный host keepalive пропал >350 ms, SPAWN становится false.
- Watchdog НЕ очищает framebuffer.
- Private command не пишет EEPROM.
- Custom effect выбирается через `rgb_matrix_mode_noeeprom()` только когда ещё не выбран.
- Host не посылает 0x24 per-LED stream.
