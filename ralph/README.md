# Ralph

An agent loop for working the DDD refactor ticket chain: same prompt, fresh context, **one ticket per iteration**. State lives in git and Linear, not in the agent's head — which is what makes a fresh context per iteration work at all.

```
ralph/
├── PROMPT.md      the standing prompt — the whole contract
├── settings.json  the tool allow-list an unattended run needs
├── format.jq      renders the event stream into readable console output
├── once.sh        one iteration, then stop
├── loop.sh        repeated iterations until the chain runs dry
├── log.md         append-only ledger, one line per iteration
└── runs/          per-iteration transcripts, .log + .jsonl (gitignored)
```

## Run it

```sh
./ralph/once.sh          # one ticket
./ralph/loop.sh 5        # up to five tickets, sequentially
touch ralph/STOP         # ask a running loop to finish after the current iteration
```

Exit codes, which `loop.sh` acts on:

| Code | Meaning | Loop |
| --- | --- | --- |
| 0 | `RALPH_DONE` — ticket finished | continues |
| 3 | `RALPH_NO_TICKETS` — queue empty | stops, success |
| 4 | `RALPH_PARTIAL` / `RALPH_BLOCKED` | stops — the next iteration would just inherit the same stuck ticket |
| 5 | no sentinel printed | stops — unknown state |
| other | `claude` itself failed | stops |

## Console output

Runs stream live: each tool call appears as `→ Bash dotnet test`, each result as `← …` (or `✗ …` when it failed), and the final summary under `───── result ─────`. Two files per iteration land in `ralph/runs/`: the readable `.log` you saw, and the raw `.jsonl` event stream for when that isn't enough.

`RALPH_QUIET=1` reverts to final-text-only. Streaming needs `jq`; without it the scripts fall back to plain text automatically.

## Permissions — read this before leaving it alone

Unattended runs use `ralph/settings.json` — an explicit allow-list (Linear MCP, `git`, `gh`, `dotnet`, and read-only shell tools) plus a small deny-list (force-push, hard reset, `rm -rf`, `curl`). Anything not on the list is **denied outright** rather than prompting, via `--permission-prompts none`, because a permission prompt in a non-interactive run is a dead end.

This is worth knowing because the first unattended run did nothing at all: `--permission-mode acceptEdits` covers *file edits only*, so every `Bash` and MCP call was refused and the agent — correctly — reported itself blocked rather than faking progress.

If Ralph reports a denied tool it genuinely needs, add that one entry to `ralph/settings.json`. Keep the file narrow; it is the security boundary.

### The rtk rewrite, and why the allow-list looks odd

A global `PreToolUse` hook rewrites shell commands through `rtk` for token savings, and for most commands it returns "ask" — meaning it *wants* a prompt. Unattended, a prompt is a denial, so the second Ralph run got `Bash` refused session-wide even though `git` and `cat` were both on the allow-list: by the time permissions were evaluated the command had become `rtk git status`, which `Bash(git:*)` does not match.

Hence `Bash(rtk:*)` in the allow-list, and hence every dangerous entry appearing **twice** in the deny-list — `git push --force` and `rtk git push --force` — because the rewrite would otherwise slip straight past a deny rule. `dotnet` and `adb` are passed through unrewritten, so they only need their plain form. Deny beats allow, so the mirrored entries hold even with `Bash(rtk:*)` allowed.

If you ever remove the rtk hook, the `rtk` entries become dead weight but harmless.

`RALPH_YOLO=1` swaps all of that for `--dangerously-skip-permissions`. That is a loaded gun: any command, no questions. The prompt still forbids committing to `main`, force-pushing and self-merging, but a prompt is a guardrail, not a sandbox — use it only where you can throw the whole checkout away.

## What one iteration does

1. Finds the lowest-numbered Linear issue in team REN that is `ready-for-agent`, unassigned, not done, and has all blockers `Done`. None → prints `RALPH_NO_TICKETS` and stops.
2. Claims it — assign to self, set `In Progress` — before touching code. That claim is the only thing preventing two instances doing the same ticket.
3. Reads the ticket, the relevant section of `docs/architecture/ddd-refactor.md`, `CONTEXT.md`, `CLAUDE.md`.
4. Branches `ralph/ren-<n>-<slug>`, implements **only that ticket**.
5. Runs the tests and both platform builds, checks each acceptance criterion individually.
6. Commits, pushes the branch and opens a PR with `gh` — one PR per ticket, linking the issue. It never merges its own PR and never force-pushes.
7. Comments on the ticket, ticks only the criteria it actually verified, sets `Done` only if all of them pass — otherwise leaves it `In Progress`, unassigned, with the blocker written down.
8. Appends a line to `ralph/log.md`, prints its sentinel, and stops.

## Why the chain barely parallelises

REN-55 → 56 → 57 → 58 → 59 → 60 → {61 → 62 → 63} is almost linear, because each ticket reshapes the same small codebase. So `loop.sh` is sequential by design — two instances would collide on the same files, and the second would be working against a `main` that the first has already moved. If you want parallelism, it belongs at the *review* stage, not here.

## Reviewing the output

Each iteration leaves a PR, so review there:

```sh
gh pr list
gh pr diff <n>
cat ralph/log.md
```

Merging is yours — in ticket order, since the chain is sequential. Full transcripts are in `ralph/runs/`.

## When to distrust it

- A ticket closed with criteria ticked but no evidence in the comment of *how* they were verified — particularly the ones needing an emulator, which the agent usually cannot run.
- A diff that touches files outside the ticket's scope. Several tickets deliberately forbid nearby fixes (REN-62 must leave the notification-id bug alone, REN-57 must preserve a call order that looks wrong); a "helpful" cleanup there is a defect.
- Any behaviour change outside REN-63. The refactor is behaviour-frozen; REN-63 is the single sanctioned exception.
