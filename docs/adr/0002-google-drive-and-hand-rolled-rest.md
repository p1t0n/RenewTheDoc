---
status: accepted
date: 2026-09-09
---

# Google Drive `appDataFolder`, reached by hand-rolled REST

Google Drive's hidden per-app folder is the launch sync target, chosen for **reach** — a sync feature the user cannot use for want of an account is worth nothing, and Google-account presence is near-universal on Android. The `drive.appdata` scope is non-sensitive (no verification, no user cap), and the folder is hidden so users cannot casually break it.

**We do not use `Google.Apis.Drive.v3`.** It is officially unsupported on mobile and its `GoogleWebAuthorizationBroker` throws at runtime there; the surface we need is about five REST endpoints, so the library's unsupportedness stops being our problem the moment we stop depending on it. Tokens come from platform-native sign-in (Play Services / Credential Manager on Android, GoogleSignIn on iOS), which also sidesteps Google's deprecated Android custom-URI-scheme redirect.

## Considered options

- **OneDrive / Microsoft Graph** is the better engineering story on every axis except reach: MSAL.NET ships real MAUI target frameworks with documented support, and Graph has ETags and `If-Match`. We traded engineering comfort for addressable users, knowingly.
- **Dropbox** has the best sync primitives of the three — real compare-and-swap and a longpoll change feed — but is capped at 500 users with a mandatory review at 50 linked users. Disqualified for a shipped app.

## Consequences

- **A provider seam exists from day one** (list / read / write / delete / change probe, storage only, auth excluded), with exactly one implementation. A second provider gets built only when users ask, or when Google removes the custom URI scheme — a deprecated path whose removal would break installed users remotely.
- **`appDataFolder` is scoped per Cloud project, not per OAuth client ID**, which is what lets Android and iOS see each other's data. This is established by entailment from primary sources, not by an explicit Google statement — the build effort must confirm it empirically first (spec §9).
- **Disconnecting the app may not delete the hidden data.** Google's docs, revocation docs and consumer pages contradict each other and user reports. So the app ships its own permanent "delete my cloud backup", calls it before revoking, and never assumes the folder is empty on a fresh install.
