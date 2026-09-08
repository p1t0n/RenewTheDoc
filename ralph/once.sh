#!/usr/bin/env bash
# One Ralph iteration: pick the next unblocked ready-for-agent ticket, finish it, stop.
#
#   ./ralph/once.sh                 # live console output; allow-list from ralph/settings.json
#   RALPH_YOLO=1 ./ralph/once.sh    # bypass all permission checks (see README)
#   RALPH_QUIET=1 ./ralph/once.sh   # final text only, no live stream
#   RALPH_MODEL=opus ./ralph/once.sh
#
# Exit codes:
#   0  ticket completed        (RALPH_DONE)
#   3  nothing takeable        (RALPH_NO_TICKETS)
#   4  ticket left unfinished  (RALPH_PARTIAL / RALPH_BLOCKED)
#   5  no sentinel printed     (agent stopped in an unknown state)
#   *  claude itself failed

set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

# `adb` and `emulator` are not on the login PATH, so a bare `adb devices` fails
# with 127 and the allow-list entry for it never matches. Put the SDK tools on
# PATH for the iteration instead of scattering absolute paths through the prompt.
ANDROID_SDK="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
export PATH="$ANDROID_SDK/platform-tools:$ANDROID_SDK/emulator:$PATH"

PROMPT_FILE="ralph/PROMPT.md"
SETTINGS_FILE="ralph/settings.json"
FORMAT_FILE="ralph/format.jq"
MODEL="${RALPH_MODEL:-}"
OUT_DIR="ralph/runs"
mkdir -p "$OUT_DIR"

STAMP="$(date +%Y%m%d-%H%M%S)"
OUT="$OUT_DIR/$STAMP.log"        # human-readable, what you saw on the console
RAW="$OUT_DIR/$STAMP.jsonl"      # raw event stream, for when the readable one isn't enough

if [[ -f ralph/STOP ]]; then
  echo "ralph: ralph/STOP present — refusing to run. Remove it to continue."
  exit 3
fi

for f in "$PROMPT_FILE" "$SETTINGS_FILE"; do
  [[ -f "$f" ]] || { echo "ralph: missing $f" >&2; exit 2; }
done

args=(-p "$(cat "$PROMPT_FILE")")
[[ -n "$MODEL" ]] && args+=(--model "$MODEL")

if [[ "${RALPH_YOLO:-0}" == "1" ]]; then
  # Nothing is gated. Only for throwaway environments.
  args+=(--dangerously-skip-permissions)
else
  # Pre-approve exactly the tools Ralph needs, and deny anything else outright
  # rather than prompting — a permission prompt in a non-interactive run is a
  # dead end. `--permission-mode acceptEdits` alone does NOT cover Bash or MCP
  # calls; that was the bug that made the first unattended run accomplish nothing.
  args+=(--settings "$SETTINGS_FILE" --permission-mode acceptEdits --permission-prompts none)
fi

# Live output: stream the event log through the formatter so tool calls and their
# results are visible as they happen. Falls back to plain text if jq is missing.
STREAM=1
[[ "${RALPH_QUIET:-0}" == "1" ]] && STREAM=0
command -v jq >/dev/null || STREAM=0

echo "ralph: starting iteration $STAMP"
echo "ralph: log $OUT"
[[ $STREAM -eq 1 ]] && echo "ralph: raw $RAW"
echo

set +e
if [[ $STREAM -eq 1 ]]; then
  claude "${args[@]}" --output-format stream-json --verbose < /dev/null 2>&1 \
    | tee "$RAW" \
    | jq -r --unbuffered -f "$FORMAT_FILE" 2>/dev/null \
    | tee "$OUT"
  status=${PIPESTATUS[0]}
else
  claude "${args[@]}" < /dev/null 2>&1 | tee "$OUT"
  status=${PIPESTATUS[0]}
fi
set -e

# When streaming, sentinels live in the raw event log as well; grep both so a
# formatter hiccup can't lose the outcome.
SEARCH=("$OUT")
[[ $STREAM -eq 1 && -f "$RAW" ]] && SEARCH+=("$RAW")

if [[ $status -ne 0 ]]; then
  echo
  echo "ralph: claude exited $status — see $OUT"
  exit "$status"
fi

echo
if grep -qh "RALPH_NO_TICKETS" "${SEARCH[@]}"; then
  echo "ralph: no takeable tickets."
  exit 3
elif grep -qhE "RALPH_(PARTIAL|BLOCKED)" "${SEARCH[@]}"; then
  echo "ralph: ticket left unfinished:"
  grep -ohE "RALPH_(PARTIAL|BLOCKED)[^\"]*" "${SEARCH[@]}" | head -1
  exit 4
elif grep -qh "RALPH_DONE" "${SEARCH[@]}"; then
  grep -ohE "RALPH_DONE[^\"]*" "${SEARCH[@]}" | head -1
  exit 0
else
  echo "ralph: no sentinel in output — agent stopped in an unknown state. Read $OUT."
  exit 5
fi
