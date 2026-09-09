# DDD Refactor — Architecture Spec

Status: **locked**. Produced by wayfinder map [REN-43](https://linear.app/renewthedoc/issue/REN-43); every decision below traces to a closed ticket on that map, where the rejected alternatives and their reasons are recorded.

Behaviour is **frozen** with exactly one granted exception (§8, step 9). This document is the input to `/to-tickets`; the section numbering is the intended ticket granularity.

---

## 1. Why

Two concrete problems, not a style preference:

1. **Domain rules live in the UI.** CONTEXT.md's headline rule — editing a Document cancels its Reminder and plans a new one — is implemented in `AddDocumentPage.xaml.cs:174-181`, with delete orchestration duplicated in `AddDocumentPage.xaml.cs:192` and `DocumentListPage.xaml.cs:158`. None of it is tested, because page code-behind in a MAUI head cannot be tested by a plain test project.
2. **A platform adapter is making domain decisions.** `LocalNotificationReminderScheduler.cs:16` calls `ReminderPlanner.Plan(document, DateTime.Now)` — reminder policy *and* an ambient clock read, inside the notification adapter.

Cloud Sync (REN-29) will be built on top of this code, which raises the cost of leaving it as it is.

## 2. Context map

Two bounded contexts.

| Context | Status | Contents |
| --- | --- | --- |
| **Documents** | this refactor | Document, Owner, Country, Remind-Before, Reminder (derived), document states |
| **Cloud Sync** | later, REN-29 | Cloud Connection, mirror files, revisions, tombstones |

`Cloud Sync → Documents`, never the reverse. The Documents context contains **zero** sync types. Reminders are not a third context: a Reminder exists only as a consequence of a Document.

## 3. Aggregates

### 3.1 Document — aggregate root

Single-entity aggregate. Owner is referenced by id across the boundary.

**Invariants, enforced at construction and on every edit:**

| Rule | `DomainRule` code |
| --- | --- |
| Name non-empty after trim | `DocumentNameRequired` |
| Name ≤ 200 characters | `DocumentNameTooLong` |
| Country is two ASCII letters, stored uppercase | `CountryCodeInvalid` |
| Remind-Before ≥ 0 days | `RemindBeforeNegative` |

**Deliberately not enforced:**

- **Country is not checked against the ISO registry.** A `RegionInfo` check would make the same Document valid on one platform and invalid on another — the code set is whatever ICU on that OS version lists, and the current picker is built from `CultureInfo.GetCultures(SpecificCultures)` rather than the registry — plus it drags `System.Globalization` into the domain with real trim/AOT exposure. A hard-coded ~250-entry table would need maintaining forever to guard an optional field whose only consumer is a display name. Shape-only is the invariant that is actually true.
- **No Remind-Before upper bound.** "Remind me absurdly early" is legal user intent; a cap would be a behaviour change.
- **No Expiry Date bounds.** Expired Documents are explicitly legal (CONTEXT.md).
- **No cross-field rule between Remind-Before and Expiry Date** — no issue date is stored, so there is nothing to relate.

**API:**

```csharp
static Document Create(...)                 // new id, validates
static Document Restore(...)                // existing id, validates (see §5.2)
Document Edit(...)                          // returns a new validated instance
DocumentState StateOn(DateOnly today)
ReminderInstruction PlanReminder(DateTime nowLocal)
```

One **composite** `Edit`, not per-field methods: the UI has a single atomic save, and seven methods where only one composite call happens is ceremony. The split into `Rename` / `MoveExpiry` / `ChangeRemindBefore` / `AssignOwner` / `SetCountry` / `EditNote` becomes correct the day per-field editing or per-field sync merging exists — recorded as fog on the map, not built now.

`Edit` **returns a new instance**; the aggregate is immutable. An immutable aggregate cannot be left half-edited when validation throws part-way through.

### 3.2 Reminder — derived, never stored

`ReminderPlanner` is deleted; the aggregate owns the derivation via `PlanReminder(nowLocal)`. **Nothing reminder-shaped is persisted** — no scheduled-at, no fired flag, no reminder rows.

A stored Reminder entity was rejected: it buys real capability (reconciliation, "did it fire?", counting against the iOS 64-pending cap) but it is new state, therefore new behaviour and a new sync payload.

Two consequences, both settled:

- The **iOS 64-pending-notification cap is an adapter concern.** The domain has no notion of scheduling capacity.
- **Notification text stays adapter-side.** Localisation does not enter the domain.

`ReminderInstruction.None | Immediate | At(LocalTime)` keeps its name, deliberately against the binding-language rule: it is a directive crossing the port to the scheduler, not the Reminder the user experiences. `Reminder.None` would read as "a Reminder that is None". CONTEXT.md was sharpened instead (§7).

### 3.3 Owner — its own aggregate root

Identity is independent of any Document: rename the person and every Document's owner is still that person.

- **Invariants:** Name non-empty after trim (`OwnerNameRequired`), ≤ 100 characters (`OwnerNameTooLong`).
- **Name uniqueness is not an invariant.** Two people can share a name, duplicates are legal today, and enforcing uniqueness needs a repository query before every save — persistence dragged into the domain. The resulting UX problem (duplicates are indistinguishable in picker and chips) is recorded as fog.
- **API: creation only.** No `Rename` method until something calls it.
- **Referential rule (decided, not implemented): an Owner cannot be removed while any Document references it.** The Document must be reassigned first. *Orphan-to-Me* was rejected because it silently rewrites meaning — "Anna's passport" becomes mine — and under sync that rewrite propagates to every device with no user action. *Cascade* was rejected because it destroys Documents behind a dictionary edit. Forbid is also the only option needing no cross-aggregate tombstone fan-out.

Enforcement of that rule is an **application-service** concern, not an aggregate invariant — so a Document referencing an unknown Owner is a representable, valid aggregate (which matters for sync, §6.5).

### 3.4 Document owner — `DocumentOwner` value object

```csharp
abstract record DocumentOwner
{
    sealed record Person(OwnerId Id) : DocumentOwner;
    static DocumentOwner Me { get; }   // singleton instance, not allocated per row
}
```

Replaces `Guid? OwnerId` where null meant "Me". The model has **three** cases — All / Me / that person — and one nullable `Guid` carries two; today `DocumentListPage.xaml.cs:29` needs a second variable plus a comment to disambiguate, and `AddDocumentPage.xaml.cs:70-76` rebuilds the same triple through picker index arithmetic. Storage stays a nullable Guid column.

Making Me a real dictionary row was rejected: CONTEXT.md states Me is not a dictionary entry, and a seeded row is one more thing sync must reconcile per device.

## 4. Application-service seam

`DocumentAppService` and `OwnerAppService` — one per aggregate. Six use cases across two aggregates is below the threshold where per-use-case classes pay (that threshold: a use case with its own collaborators or its own authorization).

Command objects with a dispatcher were rejected twice over: below the ceremony ceiling, and the only AOT-safe dispatcher is hand-rolled — `MakeGenericType` over unseen instantiations **crashes** under iOS full-AOT (`ExecutionEngineException: ... aot-only mode`), verified in REN-53.

**Use cases:** add Document, edit Document, delete Document, list Documents (grouped + filtered), list Owners, add Owner, ensure notification permission, re-plan all reminders (§8 step 9).

**Reads go through the same service**, returning already-grouped domain results with owner/status filters as plain arguments. One collaborator per page; `DocumentOwner` expresses All / Me / that person as a single parameter.

**Errors: exceptions.** `DomainRuleViolationException` carrying a `DomainRule` enum; pages catch and show the existing localized alert. `Result<T>` is a ceiling increase — C# 14 / .NET 10 ship no `Result<T>` and no discriminated unions, so it would be ours to own forever, and the page's only response to failure is `DisplayAlert`.

**Exceptions carry error codes, never English strings**, so `L.T` can localise them.

### 4.1 `IReminderScheduler` port

```csharp
Task ScheduleAsync(DocumentId id, ReminderInstruction instruction, ReminderContent content);
Task CancelAsync(DocumentId id);
Task<bool> EnsurePermissionAsync();
```

The app service asks the aggregate to plan, then hands the adapter a decided instruction. The adapter does **zero** domain work, and "edit = cancel + re-plan" becomes testable with no platform present.

Permission joins the port: it is currently a public static on the concrete adapter called from `DocumentListPage.xaml.cs:24`, which defeats the seam.

The Guid→int notification id mapping stays adapter-internal and unchanged. **It is a known bug** — `id.GetHashCode() & 0x7FFFFFFF` squeezes 128 bits into 31, so two Documents can collide and one's cancel silently kills the other's reminder, permanently (Guid hashing is deterministic). Filed as [REN-54](https://linear.app/renewthedoc/issue/REN-54), **outside** this refactor, so a behaviour fix does not hide inside a structural diff.

## 5. Persistence

### 5.1 Contracts

```csharp
Task<Document?> GetAsync(DocumentId id);
Task<IReadOnlyList<Document>> GetAllAsync();
Task SaveAsync(Document document);     // upsert
Task RemoveAsync(DocumentId id);
```

Same shape for `IOwnerRepository`. Upsert rather than Add + Update: today's split has two unhandled asymmetries — `UpdateAsync` on a missing row silently affects **0 rows**, `InsertAsync` on an existing id **throws** — and the app service already knows create-from-edit.

`GetAllAsync` survives; `DocumentList.Grouped` needs the whole set and a collection is tens of rows. No read model.

**The repository rejects a `default` id explicitly** — `default(DocumentId)` is an empty Guid the struct cannot refuse.

### 5.2 Reconstitution — fail loud

`Document.Restore` **re-runs invariants and throws** on a bad row. "Reconstitution does not re-validate" could not be sourced to Evans or Vernon (REN-46), so there is no authority to defer to, and the app is unreleased, so no legacy row exists that a stricter rule would reject.

`RuntimeHelpers.GetUninitializedObject` is available — REN-53 verified it AOT-safe at runtime on an IL-stripped iOS AOT build — and is deliberately **not used**: bypassing construction is how an invalid aggregate would circulate. Silent repair or clamping was rejected outright as unauditable data loss.

### 5.3 Row mapping

Private nested `*Row` POCOs inside the adapter, **every property `{ get; set; }`**, and **a test that asserts the created table's actual column list**.

That test is load-bearing, not hygiene. REN-53 verified that sqlite-net maps `init`-only properties fine, but **silently skips get-only and `private set` properties**: a Row mixing them produced a table of exactly **one column** — no exception, no warning, data gone. That failure is invisible in code review, so the guard must read the schema back. (Positional records do not compile against sqlite-net's `new()` constraint at all.)

The domain stays free of persistence attributes.

### 5.4 Connection and initialization

`SqliteDatabase` singleton owns `SQLiteAsyncConnection` and **one explicit `InitializeAsync()` awaited at startup**. `SqliteDocumentRepository` and `SqliteOwnerRepository` take it as a dependency.

Today `EnsureInitializedAsync` guards every single call (`SqliteDocumentStore.cs:15,22,29,36,43,50`) and two concurrent first calls can both create tables — a latent race. The startup re-plan pass already requires the database open before the UI appears, so an ordered init point exists; this deletes six guards and the race together.

`SqliteDatabase` exposes its connection so a second context can create its own tables in the same file (§6.6).

### 5.5 Schema — no reshape

Same tables, same columns. Reminder stores nothing; `DocumentOwner` collapses to the existing nullable Guid; Country stays a two-character string. Reshaping in anticipation of sync was rejected — REN-29's mirror is one JSON file per Document and may want no columns at all.

### 5.6 `CancellationToken` parameters are dropped

Every port method takes one today and **no implementation passes it anywhere** — sqlite-net's async API accepts no token, and every use case is a single-row write behind a button tap. A token the implementation cannot honour is a lie the compiler endorses.

## 6. Tactical patterns — what earns its place

| Pattern | Verdict |
| --- | --- |
| Typed ids (`DocumentId`, `OwnerId`) | **in** — `readonly record struct`, unwrapped in Row mapping |
| `Country` value object | **in** — sealed class used as `Country?` |
| `RemindBefore` value object | **kept** — already exists |
| `DocumentName`, `Note` VOs | **out** — rules enforced in the factory; wrappers would ripple into every XAML binding for no extra guarantee |
| Private ctor + named factories | **in** — `Create` / `Restore` |
| Repository per aggregate | **in** |
| Internal domain events | **struck** — see below |
| `Result<T>` | **out** — ceiling increase |
| Specifications, unit of work, CQRS | **out** — never on the table |

**Typed ids cannot reach the database.** sqlite-net has no converter seam and never did (`ITextSerializer` belongs to a different fork, `[TextBlob]` to SQLiteNetExtensions, PR #534 closed unmerged in 2017). They live in the domain and unwrap at the `*Row` boundary — which already exists, so the cost is contained. `.Value` is needed at sqlite-net call sites because `Get<T>(object pk)` binds through a type switch.

**`Country` is a sealed class, not a struct.** Absence must be representable, and `default(struct)` bypasses every guard (unfixed in C# 14, csharplang #146) — a struct `Country` would carry a silent invalid state no guard could catch. `Country?` sidesteps it.

**`RemindBefore` stays a struct.** Its `default` is 0 days — "remind me on the expiry date itself" — a legitimate value, so the hole has nothing to leak. This is the one place the `default(struct)` warning does not bite; stated explicitly so nobody converts it for symmetry.

**`required` members are not the enforcement mechanism.** REN-46 established they are compile-time only, with no defence against the reflection path sqlite-net materialises rows through. They remain a convenience.

**Domain events are struck from the ceiling** — considered and rejected, recorded so the allowance is not re-litigated. The seam orchestrates two collaborators explicitly, which is easier to read and to test; and the only AOT-safe dispatcher is hand-rolled code to own for zero present benefit.

## 7. Layout, naming, glossary

### 7.1 Projects

| Project | TFM | Contents |
| --- | --- | --- |
| `RenewTheDoc.Domain` | `net10.0` | aggregates, VOs, typed ids, `DocumentState`, `DocumentList`, `ReminderInstruction`, `DomainRule`, `DomainRuleViolationException`, ports |
| `RenewTheDoc.Application` | `net10.0` | `DocumentAppService`, `OwnerAppService` |
| `RenewTheDoc.Persistence` | `net10.0` | `SqliteDatabase`, both Sqlite repositories, `*Row` types; owns `sqlite-net-pcl` |
| `RenewTheDoc.App` | `net10.0-android`/`-ios` | pages, XAML, `LocalNotificationReminderScheduler`, localisation, DI |

`App → Application + Persistence → Domain`. Nothing points back.

**The Persistence split is forced, not chosen:** `RenewTheDoc.App` targets platform TFMs, so a plain test project cannot reference it — which is why REN-53's spike had to build a throwaway project to test sqlite-net at all. Application stays its own assembly so "the domain must not reference a service" is a compiler error rather than a review habit. Verified: the MAUI head already references a plain `net10.0` library today, so the split needs no MAUI accommodation. Solution is `.slnx`; new entries go in the existing `/src/` block.

**Namespaces:** `RenewTheDoc.<Layer>.Documents`. The context segment looks redundant with one context — that is the point: Cloud Sync arrives as `RenewTheDoc.Domain.Sync` without renaming anything shipped.

**Tests, mirroring source:** `RenewTheDoc.Domain.Tests` (renamed from `Core.Tests`), `RenewTheDoc.Application.Tests` (fakes for repositories + scheduler; characterization tests land here), `RenewTheDoc.Persistence.Tests` (real SQLite; home of the schema guard test).

### 7.2 Renames

| Today | Becomes |
| --- | --- |
| `RenewTheDoc.Core` | `RenewTheDoc.Domain` |
| `IDocumentStore` | `IDocumentRepository` |
| `IOwnerStore` | `IOwnerRepository` |
| `SqliteDocumentStore` | `SqliteDocumentRepository` + `SqliteOwnerRepository` + `SqliteDatabase` |
| `ReminderPlanner.Plan` | deleted → `Document.PlanReminder(nowLocal)` |
| `DocumentStateExtensions.GetState` | deleted → `Document.StateOn(today)` |
| `DocumentListOrder.Sorted` | deleted → `DocumentList.Grouped(documents, today)` → `IReadOnlyList<DocumentGroup>`, `DocumentGroup(DocumentState, IReadOnlyList<Document>)` in glossary order |
| `ReminderInstruction` | unchanged (§3.2) |
| `LocalNotificationReminderScheduler` | unchanged — names the plugin it wraps |

`Document.Edit` takes its name from CONTEXT.md's own sentence ("**Editing** a Document cancels its Reminder…"). `DomainRule` and `DomainRuleViolationException` sit at the Domain root, shared by both aggregates and by Cloud Sync later.

### 7.3 Localisation

Every `DomainRule` value needs a key in **all three** resx files. `NameRequired` and `InvalidCustomDays` already exist and are reused for `DocumentNameRequired` and `RemindBeforeNegative`; new keys: `DocumentNameTooLong`, `CountryCodeInvalid`, `OwnerNameRequired`, `OwnerNameTooLong`. The code→key mapping lives in `App` — the only layer that knows `L.T` exists.

### 7.4 CONTEXT.md

Amended by this map (uncommitted in the working tree at time of writing):

- **Reminder** — restated as the user's standing request, once, at 09:00 local on (Expiry − Remind-Before); the device honours it by alerting. The implementation word "notification" moved to the adapter's side.
- **Owner / Me** — shared names explicitly allowed; Me defined as the app's user, the same person on every device connected to their own cloud storage, with the shared-folder consequence stated; the forbid-while-referenced rule recorded.

CONTEXT.md remains a glossary. No structural decisions belong in it.

## 8. Migration order

Green at every commit. Each step is a ticket.

> **Deviation from the map's stated constraint, flagged deliberately.** REN-43 required characterization tests *first*. That is impossible as written: the behaviour to pin (add/edit/delete orchestration) lives in page code-behind inside a MAUI head, which no plain test project can reference. Tests cannot precede a seam that does not exist. The sequence below therefore performs the **minimum enabling move** — a mechanical, reviewed copy of the existing call sequences into services — and pins them immediately after, before any semantic change. Steps 1–3 must land together or not at all.

| # | Step | Notes |
| --- | --- | --- |
| **0** | **Bump sqlite-net 1.9.172 → 1.11.285 and drop the `SQLitePCLRaw.bundle_green` pin** | Separate from the refactor. The pin exists only to override sqlite-net-pcl's vulnerable transitive 2.1.2 (`RenewTheDoc.App.csproj:64`); 1.11.285 pulls SQLitePCLRaw 3.0.3, making it unnecessary. Verified: builds clean on both heads, public API diff is one removed line we don't use, Android reaches **0 warnings** and NU1903 retires. **Must include an Android emulator smoke run** — the spike verified the build, never the runtime, against SourceGear's native lib. |
| **1** | Create `Domain` / `Application` / `Persistence` projects; move existing types unchanged; rename `Core` → `Domain` | Compile-only. No behaviour change, no logic edits. |
| **2** | Mechanically extract page orchestration into `DocumentAppService` / `OwnerAppService` | A **copy**, preserving today's exact sequences — including the current `Update` → `Cancel` → `Schedule` order in `AddDocumentPage.xaml.cs:178-181`. No cleanup in this step. |
| **3** | Characterization tests pinning those sequences | Fakes for both repositories and the scheduler. Assert call order and arguments as they are *today*, bugs included. |
| **4** | Domain reshaping: typed ids, `Country`, `DocumentOwner`, `Create`/`Restore`/`Edit`, `StateOn`, `PlanReminder`, `DocumentList.Grouped` | Tests move to the new API; expectations unchanged. `ReminderPlanner`, `DocumentStateExtensions`, `DocumentListOrder` deleted here. |
| **5** | Repository contracts: `Get`/`Save`/`Remove`/`GetAll`, `SqliteDatabase`, startup `InitializeAsync`, drop `CancellationToken` params, schema guard test | Adapter moves into `Persistence`. |
| **6** | Port reshape: `ScheduleAsync(DocumentId, ReminderInstruction, ReminderContent)`, permission onto the port; adapter stops planning | The point of the whole exercise — after this, no domain logic remains outside `Domain`. |
| **7** | `DomainRuleViolationException` + `DomainRule`; page-side code→resx mapping; four new keys × three files | `RemindBefore` stops throwing `ArgumentOutOfRangeException`. |
| **8** | Renames sweep + CONTEXT.md commit | Mechanical. |
| **9** | **Startup reminder re-plan pass** | **The one granted behaviour change.** Its own ticket, never folded into a structural step. Re-plans every Document's reminder on launch, covering losses nothing can catch at the call site: scheduler failure after a successful write, revoked permission, restore-to-new-device, the iOS 64-cap truncating silently. |

Out of scope throughout: MVVM adoption, the REN-54 notification-id fix, Owner lifecycle implementation, sync protocol, any DB migration framework.

## 9. Sync extension points — constraints this refactor must not break

The finding: **the Documents context needs no new capability for sync.** Every point below is already satisfied by decisions taken for other reasons. REN-34 (sync protocol) is still open; nothing here decides it.

1. **`DocumentId` is the sync identity** — stable, never regenerated on edit. Must not be re-encoded into anything lossy (REN-54 is the cautionary tale), and the empty Guid must be rejected explicitly wherever an id is parsed from a file name or payload.
2. **No change tracking in the domain.** No `ModifiedAt` (sync bookkeeping in domain clothing), no domain events (struck), no journal written by the Documents repository (inverts the dependency). Sync diffs `GetAllAsync` against its own last-synced snapshot in its own tables. Cost: a full read per sync — nothing at tens of rows. If insufficient, sync asks for an explicit change-log seam as its own ticket.
3. **No tombstone concept in the domain.** `RemoveAsync` is a hard delete; tombstones are sync's own. A soft-delete flag would leak into every query and re-open settled questions (is a deleted Document expired? does it group? does it get a Reminder?). Known gap for REN-34: after uninstall/reinstall the snapshot is gone, so a local delete is indistinguishable from never-seen.
4. **Sync validates at its own boundary, never via reconstitution.** Because reconstitution fails loud (§5.2), an invalid row reaching SQLite surfaces on *list load* — one bad mirror file would brick the document list. A mirror file with an invalid country code is **quarantined whole**, not imported with the field dropped: silent field loss during a merge is the same class of act rejected in §3.3 and §5.2.
5. **Me is never materialised.** Me is the Cloud Connection's owner, so a null-owner Document correctly reads as "Me" on every device of that person. A dangling `Person(OwnerId)` is a valid aggregate and degrades gracefully in the UI (today's lookup already yields no prefix), so import ordering is a protocol choice, not a domain requirement.
6. **`SqliteDatabase` lets a second context create its own tables** in the same SQLite file, invoked at startup after the Documents initializer. This is the only concrete ask sync makes of this refactor.

---

## Traceability

| Ticket | Decision |
| --- | --- |
| [REN-46](https://linear.app/renewthedoc/issue/REN-46) | Research: dependency-free tactical patterns, AOT and sqlite-net constraints |
| [REN-53](https://linear.app/renewthedoc/issue/REN-53) | Spike: `init` mapping, `GetUninitializedObject` under AOT, the 1.11.285 bump |
| [REN-44](https://linear.app/renewthedoc/issue/REN-44) | Document aggregate, invariants, Reminder, time, state/grouping |
| [REN-45](https://linear.app/renewthedoc/issue/REN-45) | Owner aggregate, `DocumentOwner`, referential rule, Me under sync |
| [REN-47](https://linear.app/renewthedoc/issue/REN-47) | Application-service seam, port reshape, errors, the freeze exception |
| [REN-48](https://linear.app/renewthedoc/issue/REN-48) | Repository contracts, reconstitution, Row shape, connection, the bump |
| [REN-50](https://linear.app/renewthedoc/issue/REN-50) | Tactical-pattern inventory |
| [REN-51](https://linear.app/renewthedoc/issue/REN-51) | Projects, namespaces, tests, renames, glossary |
| [REN-49](https://linear.app/renewthedoc/issue/REN-49) | Sync extension points |
| [REN-54](https://linear.app/renewthedoc/issue/REN-54) | Notification-id collision — bug, outside this refactor |
