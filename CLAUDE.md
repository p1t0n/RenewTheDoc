# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

.NET MAUI (net10.0) mobile app that reminds you to renew documents (IDs, passports, insurance) before they expire. Android + iOS. Privacy-first: all data on-device, no backend, no analytics, no INTERNET permission on Android — don't add network dependencies **outside the sync effort**.

Network access arrives with the Sync Map (REN-29): Documents will mirror into the user's *own* cloud storage, with no backend and no accounts of ours. Until that ships the claims above hold literally; when it does, the positioning decided in REN-32 replaces them (README lead, README claims bullet, and the manifest comment all change together). So: still no analytics, no telemetry, no backend — but network calls to the cloud the user connected are expected, not a violation.

**CONTEXT.md is the canonical domain glossary** (Document, Owner, Country, Remind-Before, Reminder, document states, edit-behaves-like-recreation rule). Read it before touching domain logic; keep it in sync when domain rules change.

Work is planned as "wayfinder maps" on Linear (team REN).

## Commands

```sh
dotnet test tests/RenewTheDoc.Domain.Tests         # domain rules
dotnet test tests/RenewTheDoc.Application.Tests    # use-case orchestration, over fakes
dotnet test tests/RenewTheDoc.Persistence.Tests    # real SQLite, incl. the schema guard test
# NB: bare `dotnet test` also builds the Android head and fails without the JDK flag below
dotnet test tests/RenewTheDoc.Domain.Tests --filter "FullyQualifiedName~DocumentStateTests"  # one class

# Android build (needs JDK 17 + Android SDK)
dotnet build src/RenewTheDoc.App -f net10.0-android \
  -p:JavaSdkDirectory=$HOME/.jdks/jdk-17.0.20+8/Contents/Home \
  -p:AndroidSdkDirectory=$HOME/Library/Android/sdk
# add -p:EmbedAssembliesIntoApk=true for a standalone adb-installable debug APK

# iOS simulator build (Mac + full Xcode)
dotnet build src/RenewTheDoc.App -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64

# iOS device build without a signing identity
dotnet build src/RenewTheDoc.App -f net10.0-ios -p:RuntimeIdentifier=ios-arm64 \
  -p:EnableCodeSigning=false -p:_RequireCodeSigning=false

dotnet workload install maui                       # one-time setup
dotnet workload restore                            # fixes NETSDK1147 (workload on a stale SDK band)
```

Build gotchas, each verified the hard way (REN-53):

- `JavaSdkDirectory` needs the **full** path including `/Contents/Home` — without it, `error XA5300: The Java SDK directory could not be found.`
- `ANDROID_HOME` is unset on this machine, so Android builds need `-p:AndroidSdkDirectory` explicitly.
- `xcode-select` points at CommandLineTools, so iOS builds may need `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer`.
- Unsigned iOS **device** builds stop at `error : No valid iOS code signing keys found in keychain.` without the two code-signing opt-outs above (that gate runs before compilation).
- `error NETSDK1147: ... the following workloads must be installed: maui-android` usually means the `maui` workload is registered under an older SDK band than the installed SDK — `dotnet workload restore`, not a reinstall.

CI (`.github/workflows/ci.yml`) runs: domain tests, Release Android build, unsigned iOS simulator build.

## Architecture

Four projects + three test projects, `App → Application + Persistence → Domain`, nothing pointing back. The shape and the reasoning behind it are in [docs/architecture/ddd-refactor.md](docs/architecture/ddd-refactor.md).

- **`src/RenewTheDoc.Domain`** (plain `net10.0`) — the model (`Document`, `Owner`, `DocumentOwner`, `Country`, `RemindBefore`, typed `DocumentId`/`OwnerId`), derived state (`DocumentState`, `DocumentList.Grouped`), `ReminderInstruction`/`ReminderContent`, `DomainRule` + `DomainRuleViolationException`, and the ports `IDocumentRepository`, `IOwnerRepository`, `IReminderScheduler`. `Document` is immutable, built only through `Create`/`Restore`, edited through `Edit` returning a new instance, and **plans its own Reminder** (`PlanReminder(nowLocal)`) — there is no `ReminderPlanner`. New domain rules go here, with tests.
- **`src/RenewTheDoc.Application`** — `DocumentAppService` (add / edit / delete / list / re-plan-all / notification permission) and `OwnerAppService` (list / add). **This is where use-case orchestration lives**; pages call it and do no orchestration of their own. Time enters as a parameter (`nowLocal`) — no clock port, no ambient `DateTime.Now` below the UI.
- **`src/RenewTheDoc.Persistence`** — `SqliteDocumentRepository` + `SqliteOwnerRepository` + `SqliteNotificationNumbers`, over a shared `SqliteDatabase` that owns the connection and creates **three** tables (`DocumentRow`, `OwnerRow`, `NotificationNumberRow`) in one `InitializeAsync`, awaited once at startup in `MauiProgram` — not lazily per call. Mapping uses private nested `*Row` POCOs with fully settable properties: sqlite-net **silently skips** get-only and `private set` properties, which is why a schema guard test asserts the real column list. `DateOnly` is stored as an ISO string. No migration framework, so schema changes need care.
- **`src/RenewTheDoc.App`** — the MAUI head: pages, XAML, localization, DI, and `LocalNotificationReminderScheduler` (Plugin.LocalNotification), which executes a **decided** `ReminderInstruction` and makes no domain decisions. Notification ids come from `SqliteNotificationNumbers`, assigned once per Document and persisted — they are no longer derived from the id (REN-54; `DerivedNotificationNumber` is kept only to pin the old behaviour in tests).
- **`tests/`** mirrors source: `RenewTheDoc.Domain.Tests`, `.Application.Tests` (fakes for both repositories and the scheduler), `.Persistence.Tests` (real SQLite). No App/UI tests — page code-behind sits in the MAUI head, which a plain test project cannot reference.

Pages (`DocumentListPage`, `AddDocumentPage` — the latter doubles as the edit page) use plain code-behind, no MVVM framework, and are registered transient; app services and repositories are singletons.

Key domain rules (details in CONTEXT.md): a document has exactly one Reminder firing at 09:00 local on (expiry − remind-before); already-past reminder moment fires immediately once; expired documents get no reminder; **editing cancels and re-plans exactly like creation; deleting cancels**. Every reminder is also re-planned on launch, which is the safety net for scheduling lost to a failed write, revoked permission, or a device restore.

## Localization

English (neutral), Polish, Russian resx files in `Resources/Strings/AppStrings*.resx`. Every user-facing string goes through `L.T("Key")` / `L.F("Key", args)` (`Localization/L.cs`) or `{loc:Translate Key}` in XAML — add new keys to **all three** resx files. Locale follows the system; no in-app picker. Notification text is localized too.

## Design

"Compass" visual direction: calm-blue light/dark palette, Manrope font (Regular/SemiBold/ExtraBold), urgency-grouped list (Needs attention / Coming up / All good) with colored rail + days-left per row, pill filter chips. Design tokens live in `Resources/Styles/Colors.xaml` and `Styles.xaml` — use tokens, don't hardcode colors. `MauiProgram` strips the native Android underline from Entry/DatePicker/Picker via handler mappings; Compass fields draw their own surfaces.
