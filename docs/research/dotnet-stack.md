# .NET Cross-Platform Stack: MAUI vs Uno Platform vs Avalonia

Research for [REN-6](https://linear.app/renewthedoc/issue/REN-6) — which .NET cross-platform flavor RenewTheDoc (privacy-first document-renewal reminder app) should use. Researched 2026-08-08 against primary sources (official docs, GitHub repos/releases, NuGet).

## Recommendation: .NET MAUI

For an app whose single most critical feature is **local scheduled notifications that fire with the app not running**, .NET MAUI is the only one of the three with a maintained, purpose-built plugin for exactly that (Plugin.LocalNotification, updated June 2026 for .NET 10), plus built-in SecureStorage, Microsoft servicing through May 2027, MIT licensing, and free tooling. Uno and Avalonia both require hand-writing `UNUserNotificationCenter`/`AlarmManager` interop for the app's core feature — by their own maintainers' admission.

---

## Criterion 1 — Local scheduled notifications (app not running)

**MAUI — strong, via Plugin.LocalNotification (thudugala).**

- NuGet shows v14.1.1 released **June 6, 2026**, targeting `net10.0-android36.0`, `net10.0-ios26.0`, MIT license, 1.1M+ total downloads: <https://www.nuget.org/packages/Plugin.LocalNotification>
- Repo is actively maintained (~472 stars, 720 commits, 37 open issues — healthy, not a graveyard); supports scheduled and repeating notifications; requires .NET MAUI (Xamarin.Forms support ended May 2024): <https://github.com/thudugala/Plugin.LocalNotification>

**iOS platform realities (apply to all three frameworks):**

- **64-pending-notification limit**: per Apple Developer Forums (incl. DTS responses), an app may have at most 64 pending local notification requests; the system discards the rest. Threads: <https://developer.apple.com/forums/thread/76501> and <https://developer.apple.com/forums/thread/811171>. For a renewal-reminder app this means scheduling only the soonest ~60 reminders and re-topping-up on each app launch.
- **Force-quit**: scheduled local notifications are delivered by the system independent of app state — the "notifications don't fire after force-quit" concern applies to background fetch/silent push, not `UNCalendarNotificationTrigger` local notifications (confirmed in the same forum threads; UNUserNotificationCenter reference: <https://developer.apple.com/documentation/usernotifications/unusernotificationcenter>).
- Permission must be requested via `requestAuthorization`; Plugin.LocalNotification wraps this flow.

**Uno Platform — no first-party API.** Maintainer Jérôme Laban, in the official repo discussion (active May 2023 – July 2024, still open): *"At this time, we do not have support for toast on any mobile targets… you can use the native platforms APIs instead."* `Windows.UI.Notifications`/`ScheduledToastNotification` is not implemented for iOS/Android: <https://github.com/unoplatform/uno/discussions/12145>. The only community option, `ToastNotification.Uno`, is stalled at v0.1.7 with iOS "implemented, but not tested": <https://www.nuget.org/packages/ToastNotification.Uno/>. No 2025–2026 announcement changes this. You would write the UNUserNotificationCenter/AlarmManager code yourself in C# (feasible — Uno mobile heads are .NET for iOS/Android — but it's DIY for the app's core feature).

**Avalonia — no system notification API at all.** Avalonia's notification support (`Avalonia.Controls.Notifications`, `WindowNotificationManager`) is **in-app overlay only**: <https://docs.avaloniaui.net/docs/how-to/notifications-how-to>. Nothing in the Avalonia 12 release notes adds OS-level scheduled notifications. Same DIY interop burden as Uno, with a smaller mobile community to lean on.

## Criterion 2 — Secure local storage

**SQLite (equal across frameworks — all run on .NET for iOS/Android):**

- `sqlite-net-pcl` is alive: v1.11.285 published **July 13, 2026**: <https://www.nuget.org/packages/sqlite-net-pcl/> (repo: <https://github.com/praeclarum/sqlite-net>). `Microsoft.Data.Sqlite` is the Microsoft-maintained alternative.

**Encryption-at-rest — important negative finding:**

- `SQLitePCLRaw.bundle_e_sqlcipher` (the free community-SQLCipher bundle) is **deprecated on NuGet as "legacy… no longer maintained"** (last version 2.1.11, March 2025): <https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlcipher>
- Eric Sink's SQLitePCLRaw 3.0 notes state he **no longer distributes any no-cost encrypted SQLite builds**; recommended paths are SQLite's commercial SEE, Zetetic's commercial SQLCipher builds, or compiling open-source SQLCipher yourself: <https://github.com/ericsink/SQLitePCL.raw/blob/main/v3.md> and <https://github.com/ericsink/SQLitePCL.raw/wiki/SQLite-encryption-options-for-use-with-SQLitePCLRaw>
- Practical consequence for RenewTheDoc: don't build the architecture around free SQLCipher. Rely on OS-level file encryption (iOS Data Protection, Android file-based encryption) for the DB, and put genuinely sensitive values (document numbers) in secure key-value storage.

**Secure key-value storage:**

- **MAUI**: `SecureStorage` built in — iOS Keychain / Android `EncryptedSharedPreferences` (AES-256 GCM). Documented caveats: Android Auto Backup can restore undecryptable values (exclude the prefs file or handle the exception), and iOS Keychain entries survive app uninstall and may sync via iCloud Keychain — relevant for a privacy-first app: <https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage>
- **Uno**: implements `Windows.Security.Credentials.PasswordVault` on iOS (Keychain) and Android (AndroidKeyStore-encrypted file) — genuine first-party parity here: <https://platform.uno/docs/articles/features/PasswordVault.html>
- **Avalonia**: nothing built in (docs contain no secure-storage API); you'd hand-roll Keychain/KeyStore interop or pull in Essentials-style packages designed for other stacks.

## Criterion 3 — Single codebase: Android + iPhone + iPad

- **MAUI**: single project with `Platforms/` folders; one `net10.0-ios` target covers iPhone and iPad; `DeviceInfo.Idiom` (Phone/Tablet) enables idiom-adaptive layout, and a scaled iPhone layout on iPad works with zero extra effort. Docs: <https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device/information>
- **Uno**: equivalent single-project structure via Uno.Sdk (`Platforms/Android`, `Platforms/iOS`), WinUI adaptive triggers for idiom: <https://platform.uno/docs/articles/migrating-to-single-project.html>
- **Avalonia**: templates generate a shared project plus per-platform head projects (.Android/.iOS/.Desktop) — slightly more ceremony; Avalonia 12 added touch-first navigation (drawers, tab bars, bottom sheets): <https://avaloniaui.net/blog/avalonia-12>

All three satisfy this criterion; MAUI and Uno are marginally cleaner.

## Criterion 4 — Health in 2026

- **MAUI**: .NET MAUI 10 shipped Nov 11, 2025, servicing (monthly Patch Tuesday) until **May 11, 2027**; MAUI 9 went EOL May 12, 2026 — so target .NET 10: <https://dotnet.microsoft.com/en-us/platform/support/policy/maui>. Docs already carry net-maui-11.0 monikers for the Nov 2026 release. Repo: <https://github.com/dotnet/maui>
- **Uno**: very active — 6.x current, 6.6 shipped July 2026 with native AOT for Android/iOS; releases every 2–4 weeks: <https://github.com/unoplatform/uno/releases>
- **Avalonia**: 12.0 released **April 7, 2026** (<https://avaloniaui.net/blog/avalonia-12>), 12.1.1 in July 2026, 11.3.x still serviced: <https://github.com/AvaloniaUI/Avalonia/releases>. The 12.0 blog itself concedes that for mobile "conventional wisdom said look elsewhere" — mobile is newly-promoted to first-class, i.e., the least battle-tested of the three on iOS/Android.
- **Plugin ecosystem**: Plugin.LocalNotification and sqlite-net-pcl both shipped .NET-10-compatible releases in mid-2026 (links above). No maintenance gap currently, but Plugin.LocalNotification is effectively a single-maintainer project — see risks.

## Criterion 5 — Free/open tooling and licensing

- **Licenses**: MAUI — MIT (<https://github.com/dotnet/maui>); Uno — Apache 2.0 (<https://github.com/unoplatform/uno>); Avalonia — MIT core, but the former "Accelerate" is now a tiered commercial model (Plus $17/mo, Pro $49/mo) gating premium controls (TreeDataGrid, MediaPlayer, charts) and enhanced IDE tooling: <https://avaloniaui.net/accelerate>. Nothing mobile-essential is paywalled, but the trajectory of open-core monetization is worth noting.
- **Post-VS-for-Mac world**: MAUI's documented macOS path is VS Code + the .NET MAUI extension (auto-installs C# Dev Kit) + `dotnet workload install maui` + Xcode: <https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation>. Caveat: C# Dev Kit requires signing in with a Microsoft account (free at Community-eligibility level). All three frameworks build fine from the plain `dotnet` CLI; an Apple Developer Program membership ($99/yr) is required for device deploys/App Store regardless of framework.

## Criterion 6 — CI on GitHub Actions (framework-neutral)

- Standard GitHub-hosted runners are **free for public repos**; private repos get 2,000 free minutes/month (Free plan), with macOS consuming at roughly a **10x rate** vs Linux ($0.062/min vs $0.006/min overage): <https://docs.github.com/en/billing/managing-billing-for-your-products/about-billing-for-github-actions>
- macOS runner images ship with multiple Xcode versions preinstalled (see image manifests): <https://github.com/actions/runner-images>
- iOS signing on runners: GitHub documents importing the Apple distribution certificate and provisioning profile from encrypted secrets into a temporary keychain: <https://docs.github.com/en/actions/deployment/deploying-xcode-applications/installing-an-apple-certificate-on-macos-runners-for-xcode-development>
- Practical tip: run Android builds on Linux runners; reserve macOS minutes for iOS release builds. This criterion doesn't differentiate the frameworks.

---

## Key risks of choosing MAUI, and mitigations

1. **Plugin.LocalNotification is a single-maintainer project.** Healthy today (June 2026 release, .NET 10), but bus-factor 1. *Mitigation*: isolate notifications behind your own `IReminderScheduler` interface; the fallback is ~200 lines of direct `UNUserNotificationCenter`/`AlarmManager` C# — the exact code you'd write on day one with Uno/Avalonia anyway. MIT license permits forking.
2. **iOS 64-pending-notification cap** (platform, not framework). *Mitigation*: schedule only the nearest ~60 reminders; refresh the queue on every app foreground.
3. **Free SQLCipher path is dead** (`bundle_e_sqlcipher` deprecated; SQLitePCLRaw 3.0 ships no free crypto builds). *Mitigation*: rely on iOS Data Protection/Android FBE plus MAUI SecureStorage for sensitive fields; if true DB-level encryption becomes a hard requirement, budget for Zetetic SQLCipher or build SQLCipher from source.
4. **SecureStorage privacy edge cases**: iOS Keychain values survive uninstall and can sync to iCloud Keychain; Android Auto Backup can corrupt restored values. *Mitigation*: first-launch `RemoveAll()`, exclude the SecureStorage prefs file from Android backup (both patterns documented on the MAUI secure-storage page).
5. **MAUI 9 is already EOL** — start directly on .NET 10 (supported to May 2027) and plan annual .NET upgrades.

## Runner-up: Uno Platform — and when to reconsider

Uno is the credible second choice: Apache 2.0, very fast release cadence (6.6, July 2026, native AOT), single-project structure, and a genuine first-party secure-storage story (`PasswordVault` on Keychain/KeyStore). Reconsider in favor of Uno if: (a) Plugin.LocalNotification is abandoned — at that point MAUI loses its decisive advantage since both stacks would need hand-rolled notification interop; (b) you want a WebAssembly/web target for RenewTheDoc, which MAUI doesn't offer; or (c) you standardize on WinUI/XAML idioms across products. Avalonia is the weakest fit here — excellent desktop framework, but mobile only became "first-class" with 12.0 in April 2026, and it offers neither system notifications nor secure storage out of the box, which are RenewTheDoc's two core platform needs.
