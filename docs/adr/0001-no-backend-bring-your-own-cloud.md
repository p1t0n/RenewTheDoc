---
status: accepted
date: 2026-09-09
---

# Sync through the user's own cloud, with no backend of ours

RenewTheDoc needed multi-device sync and survival of device loss. Rather than run a backend, Documents mirror into **the user's own personal cloud storage**, so the app holds no user data, issues no accounts, and operates no server — privacy-first is strengthened rather than traded away, and the running cost is $0 forever.

## Considered options

- **A hosted backend** (Supabase, or our own ASP.NET Core API on Hetzner/Fly) — researched and recommended before the premise was re-examined. It brings accounts, passwords, email verification, in-app account deletion, GDPR exposure and a monthly bill, all to store data the user could hold themselves.
- **iCloud / CloudKit** — ruled out on facts: the entitlement requires a paid Apple Developer membership, and there is no Android path to iCloud Drive at all.

## Consequences

- **No account of ours exists**, so Apple's Sign in with Apple requirement (guideline 4.8) and in-app account deletion have no referent. Reasoned, never confirmed by Apple — accepted risk, with the review response prepared.
- **"Nothing connected" is a first-class state.** The app must be fully usable with no cloud attached, which shapes the UI: connecting is found in settings, never prompted.
- **The user's provider can read the mirrored files.** We cannot. This is disclosed in the same breath as the privacy claim rather than buried.
- Sync is limited to what a consumer file API can do — no conditional writes, no server clock, no push. ADR-0003 records what that costs.
