# RenewTheDoc

Cross-platform mobile app (Android / iOS / iPad) that reminds you to renew your documents — IDs, passports, insurances, registrations, anything with an expiry date.

- Add a document in seconds: name, expiry date, "remind before" (presets or custom days), optional note, country, and owner.
- Get a local notification before the document expires; editing a document reschedules its reminder, deleting cancels it.
- Documents belong to **owners** — you by default ("Me"), or anyone from a small dictionary you grow inline ("+ New owner…"): family members, whoever.
- The list triages itself into **Needs attention / Coming up / All good** and filters by owner and status with one tap.
- Localized in English, Polish, and Russian — UI, dates, and notification text.
- Privacy-first: all data stays on the device. No backend, no accounts, no analytics, no INTERNET permission on Android.

Planned later: country-specific renewal guidance, document scans, backup/export. See the fog sections of the completed wayfinder maps.

## Status

Core feature set working on Android and iOS (verified on emulator and simulators). Not yet distributed via stores. Work is planned and tracked as wayfinder maps on [Linear](https://linear.app/renewthedoc/team/REN) — completed so far: Bootstrap, Design, Edit/Delete + Country, Owners.

## Design

"Compass" visual direction: calm-blue light/dark palette, Manrope, urgency-grouped list with a colored rail + days-left number per row, pill filter chips. Design tokens live in `Resources/Styles/Colors.xaml` + `Styles.xaml`; the domain glossary in [CONTEXT.md](CONTEXT.md).

## Stack

.NET MAUI on .NET 10. Solution layout:

- `src/RenewTheDoc.Core` — domain model (see [CONTEXT.md](CONTEXT.md)), no MAUI dependency
- `src/RenewTheDoc.App` — MAUI app (Android + iOS); notifications via Plugin.LocalNotification behind `IReminderScheduler`, storage via sqlite-net behind `IDocumentStore`/`IOwnerStore`
- `tests/RenewTheDoc.Core.Tests` — domain rules tests

## Building locally

```sh
dotnet workload install maui
dotnet test                                        # domain tests
dotnet build src/RenewTheDoc.App -f net10.0-android \
  -p:JavaSdkDirectory=$JAVA_HOME                   # needs JDK 17 + Android SDK
```

iOS build requires a Mac with full Xcode (`net10.0-ios` target). For a standalone `adb install` of a debug APK, add `-p:EmbedAssembliesIntoApk=true`.

## License

[MIT](LICENSE)
