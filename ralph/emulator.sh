#!/usr/bin/env bash
# Boot an Android emulator headless and wait until it is usable, so an agent can
# satisfy an "verify on an emulator" criterion with one allowed command.
#
#   ./ralph/emulator.sh            # boot the first available AVD, wait for boot
#   ./ralph/emulator.sh Pixel_3a_XL
#   ./ralph/emulator.sh --stop     # kill any running emulator
#
# Exits 0 once `sys.boot_completed` is set, non-zero if no AVD exists or boot
# times out. Idempotent: if a device is already attached it returns immediately.

set -euo pipefail

SDK="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
ADB="$SDK/platform-tools/adb"
EMU="$SDK/emulator/emulator"
BOOT_TIMEOUT="${RALPH_EMULATOR_TIMEOUT:-180}"

[[ -x "$ADB" ]] || { echo "ralph: adb not found at $ADB" >&2; exit 2; }
[[ -x "$EMU" ]] || { echo "ralph: emulator not found at $EMU" >&2; exit 2; }

if [[ "${1:-}" == "--stop" ]]; then
  "$ADB" devices | awk '/^emulator-/ {print $1}' | while read -r d; do
    echo "ralph: killing $d"
    "$ADB" -s "$d" emu kill || true
  done
  exit 0
fi

if "$ADB" devices | grep -q "^emulator-.*device$"; then
  echo "ralph: emulator already attached"
  "$ADB" devices
  exit 0
fi

AVD="${1:-}"
if [[ -z "$AVD" ]]; then
  AVD="$("$EMU" -list-avds | head -1)"
fi
[[ -n "$AVD" ]] || { echo "ralph: no AVD configured — create one in Android Studio" >&2; exit 3; }

echo "ralph: booting AVD '$AVD' (headless, up to ${BOOT_TIMEOUT}s)"
"$EMU" -avd "$AVD" -no-window -no-audio -no-boot-anim -no-snapshot-save \
  >"ralph/runs/emulator-$AVD.log" 2>&1 &

"$ADB" wait-for-device

deadline=$(( SECONDS + BOOT_TIMEOUT ))
while (( SECONDS < deadline )); do
  if [[ "$("$ADB" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" == "1" ]]; then
    echo "ralph: emulator booted"
    "$ADB" devices
    exit 0
  fi
  sleep 3
done

echo "ralph: emulator did not finish booting within ${BOOT_TIMEOUT}s — see ralph/runs/emulator-$AVD.log" >&2
exit 4
