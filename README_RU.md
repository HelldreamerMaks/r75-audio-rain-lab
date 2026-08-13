# R75 Audio Spawn Rain v12.2 — GitHub SAFE BUILD LAB

## Цель

Это **только облачная компиляция и анализ**. Физическая клавиатура в процессе не используется.

Единственная функция будущего эффекта:

- общий звук есть -> разрешено рождение НОВЫХ Digital Rain капель;
- звука нет -> новые капли не рождаются;
- уже существующие капли продолжают штатно двигаться и затухать;
- никакого управления Bass/Beat/FFT/Brightness/Speed/Color.

## Почему это физически безопасный этап

GitHub Actions выполняется на удалённом Linux runner. Он не подключён к USB твоего ПК и не имеет доступа к R75.
В workflow нет qmk flash, make ...:flash, dfu-util download или wb32-dfu write/download команд.
Проект дополнительно запускает `scripts/audit_project_safety.py` и останавливается, если такие команды появятся в исполняемых workflow/script файлах.

## ВАЖНО: два этапа, запускать по порядку

### Этап 1 — только исходный fork

1. Создай на GitHub новый пустой репозиторий, например `r75-audio-rain-lab`.
2. Распакуй этот ZIP.
3. Загрузи **содержимое** папки в репозиторий, включая `.github`.
4. Открой вкладку `Actions`.
5. Выбери:
   `01 R75 SAFE source audit + stock baseline compile (NO FLASH)`
6. Нажми `Run workflow`.
7. После завершения скачай artifact `R75_PHASE1_SAFE_AUDIT_BASELINE`.

На этом этапе исходники fork вообще не патчатся. Action только:
- фиксирует commit ветки `OpenSignalRGB-2026`;
- проверяет `rk/r75/iso`;
- проверяет WB32FQ95 / wb32-dfu / 342D:E483;
- считает ISO/ANSI RGB entries;
- проверяет SignalRGB 1.0.5 и routing;
- запускает обычный `qmk compile` для исходного ISO VIA target;
- сохраняет лог/метаданные.

### Этап 2 — candidate81, всё ещё ТОЛЬКО В ОБЛАКЕ

**Не запускать, пока мы не посмотрели artifact этапа 1.**

Action:
`02 R75 SAFE AudioSpawn candidate81 compile (NO FLASH)`

Он применяет только keymap-level overlay:
- `audio_rain.c`
- `rgb_matrix_user.inc`
- две строки в `rules.mk`
- keymap-local `config.h`, который временно задаёт `RGB_MATRIX_LED_COUNT 81` только для candidate cloud build.

Никакой QMK core файл не редактируется.

## Почему есть 80/81 blocker

Проверяемый fork сейчас содержит противоречие:
- shared `keyboards/rk/r75/config.h` -> `RGB_MATRIX_LED_COUNT 80`;
- ISO `keyboard.json` -> 81 RGB layout entry;
- ANSI `keyboard.json` -> 80 RGB layout entries;
- ISO SignalRGB plugin E483 использует индексы 0..80, то есть 81 LED.

Именно поэтому этап 1 нужен до candidate build.

## Карантин firmware

Даже если этап 2 успешно соберёт firmware, artifact намеренно переименует результат:

`QUARANTINE_NOT_FOR_FLASH__<имя>.hex.disabled`

То есть workflow **не публикует обычный `.hex`**. Это файл только для дальнейшего анализа.

## Raw HID routing

Exact fork запускает OpenRGB routing активным. Host V12.2 сначала отправляет штатный SignalRGB GET `0x22` и проверяет protocol `1.0.5`; fork этим переключает routing на SignalRGB. После этого приватная `0x29` может дойти до keymap `via_command_kb()` без изменения QMK core.

## Что прислать мне после этапа 1

Лучше всего просто загрузи сюда скачанный ZIP artifact `R75_PHASE1_SAFE_AUDIT_BASELINE`.
Мы разберём:
- `SOURCE_AUDIT.txt`
- `BASELINE_COMPILE.log`
- `QMK_INFO_ISO.json`
- `baseline_stock_postbuild.txt`
- `FORK_COMMIT.txt`

И только после этого решим, запускать ли этап 2.

## НИЧЕГО НЕ ПРОШИВАТЬ

Этот пакет не является разрешением на flash.
До реального hardware этапа отдельно нужны stock E483 backup и подтверждённый recovery/DFU путь.
