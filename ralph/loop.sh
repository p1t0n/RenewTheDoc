#!/usr/bin/env bash
# Iterated Ralph: run once.sh repeatedly, each iteration a fresh context, until the
# ticket chain is exhausted, the iteration cap is hit, or someone drops a STOP file.
#
#   ./ralph/loop.sh                    # default cap of 10 iterations
#   ./ralph/loop.sh 3                  # cap of 3
#   RALPH_YOLO=1 ./ralph/loop.sh 9     # unattended (see README before using)
#   touch ralph/STOP                   # asks the loop to finish after the current run
#
# The cap exists so a misbehaving iteration cannot burn tokens all night. There is no
# "run forever" mode on purpose.

set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

MAX="${1:-10}"
SLEEP="${RALPH_SLEEP:-5}"

if ! [[ "$MAX" =~ ^[0-9]+$ ]] || (( MAX < 1 )) || (( MAX > 50 )); then
  echo "usage: ralph/loop.sh [iterations 1-50]" >&2
  exit 2
fi

rm -f ralph/STOP

echo "ralph: loop starting, up to $MAX iterations"
done_count=0

for (( i = 1; i <= MAX; i++ )); do
  if [[ -f ralph/STOP ]]; then
    echo "ralph: STOP file found — stopping after $done_count iteration(s)."
    break
  fi

  echo
  echo "───── ralph iteration $i/$MAX ─────"

  set +e
  ./ralph/once.sh
  status=$?
  set -e

  case $status in
    0)
      done_count=$(( done_count + 1 ))
      ;;
    3)
      echo "ralph: nothing left to do — stopping after $done_count iteration(s)."
      exit 0
      ;;
    4)
      # Ticket left partial or blocked. Continuing would just hand the next
      # iteration the same stuck ticket, so stop and let a human look.
      echo "ralph: iteration $i did not finish its ticket — stopping."
      echo "ralph: completed $done_count iteration(s) before that."
      exit 4
      ;;
    5)
      echo "ralph: iteration $i ended with no sentinel — unknown state, stopping."
      echo "ralph: completed $done_count iteration(s) before that."
      exit 5
      ;;
    *)
      echo "ralph: iteration $i failed (exit $status) — stopping so the failure is visible."
      echo "ralph: completed $done_count iteration(s) before the failure."
      exit "$status"
      ;;
  esac

  (( i < MAX )) && sleep "$SLEEP"
done

echo
echo "ralph: loop finished — $done_count iteration(s)."
echo "ralph: review the open PRs ('gh pr list'), and 'cat ralph/log.md'."
