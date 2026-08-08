# RenewTheDoc

Cross-platform mobile app (Android / iOS / iPad) that reminds you to renew your documents — IDs, passports, insurances, registrations, anything with an expiry date.

- Add a document with a name, expiry date, and a "remind before" setting in seconds.
- Get a local notification before the document expires.
- Privacy-first: all data stays on the device. No backend, no accounts, no analytics.

Planned later: country-specific document templates, initiating the renewal process online where possible, and document scans.

## Status

Early bootstrap. Work is planned and tracked on the [Linear wayfinder map](https://linear.app/renewthedoc/team/REN) (workspace `renewthedoc`, team `REN`).

## Stack

.NET MAUI on .NET 10. Solution layout:

- `src/RenewTheDoc.Core` — domain model (see [CONTEXT.md](CONTEXT.md)), no MAUI dependency
- `src/RenewTheDoc.App` — MAUI app (Android + iOS); notifications via Plugin.LocalNotification behind `IReminderScheduler`, storage via sqlite-net behind `IDocumentStore`
- `tests/RenewTheDoc.Core.Tests` — domain rules tests

## Building locally

```sh
dotnet workload install maui
dotnet test                                        # domain tests
dotnet build src/RenewTheDoc.App -f net10.0-android \
  -p:JavaSdkDirectory=$JAVA_HOME                   # needs JDK 17 + Android SDK
```

iOS build requires a Mac with full Xcode (`net10.0-ios` target).

## License

[MIT](LICENSE)
