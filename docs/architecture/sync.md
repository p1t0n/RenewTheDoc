# Cloud Sync — Architecture Spec

Status: **locked**. Produced by wayfinder map [REN-29](https://linear.app/renewthedoc/issue/REN-29); every decision traces to a closed ticket there, where the rejected alternatives and their reasoning live. Planning artifact — no production code was written on that map.

Companion ADRs: [0001](../adr/0001-no-backend-bring-your-own-cloud.md), [0002](../adr/0002-google-drive-and-hand-rolled-rest.md), [0003](../adr/0003-file-per-document-lww-tombstones.md).

---

## 1. The shape

Documents sync between a user's own devices through **the user's own personal cloud storage**. There is no backend of ours, no account of ours, and no server in the path at any point.

- **Local SQLite stays the source of truth.** Sync is offline-first; the app is fully functional with nothing connected, and that is a supported state rather than a degraded one.
- **Provider: Google Drive `appDataFolder`** — a hidden, per-application folder in the user's Drive.
- **Driver:** multi-device sync plus surviving device loss. Household sharing is explicitly not a goal; Owners remain local labels.
- **Reminders stay local, per device.** Two devices may both notify for the same Document; that is accepted (there is no push infrastructure to deduplicate through).

Glossary terms live in [CONTEXT.md](../../CONTEXT.md): **Cloud Connection** (the authorization) and **Cloud Backup** (the data in the user's cloud — current state only, no version history).

## 2. Remote layout

One small JSON file per aggregate, inside `appDataFolder`:

```
doc-{DocumentId}.json
owner-{OwnerId}.json
```

A single combined blob was rejected: with no conditional writes, two devices editing anything concurrently would silently lose a whole round of edits. A single `owners.json` fails the same way for Owners — two devices adding different people, one addition lost.

**Payload** mirrors the aggregate, plus a `schemaVersion`:

| Field | Encoding |
| --- | --- |
| `id` | Guid string |
| `name` | string |
| `expiryDate` | ISO-8601 date-only |
| `remindBeforeDays` | integer |
| `ownerId` | Guid string, **or `null` meaning Me** |
| `note` | string or null |
| `country` | two-letter code or null |
| `modifiedAt` | UTC ISO-8601 — **diagnostics only**, never the conflict comparator |
| `schemaVersion` | integer |

`ownerId: null` for Me is deliberate here even though the domain models owner as a `Me | Person` value object: the domain needed three cases (All / Me / that person), the wire format has two.

**The files stay plain, language-neutral JSON.** No .NET type discriminators, no serializer-specific encoding, no custom converters — so a future JavaScript client could read them without a .NET-shaped parser (§10).

## 3. Conflict resolution

**Per-Document last-write-wins, comparing the provider's `modifiedTime`** — the only clock both devices share.

**This is last-*upload*-wins, not last-*edit*-wins.** A phone that edited a Document offline a week ago and syncs today will overwrite an edit another device made yesterday. Comparing device-written timestamps would give the semantics we actually want, but a device with a wrong clock then wins or loses every conflict silently and permanently — a worse failure. Syncing on foreground keeps the upload gap small, which is what makes this acceptable.

**No compare-and-swap.** Drive v3 removed ETags and `If-Match`; there is nothing to condition a write on. CAS is a *rejected* option, not an open one — revisiting it requires a different provider **and** a different protocol. Consequence: two devices editing the same Document inside one sync window keep the later upload. Per-Document files already prevent the catastrophic version.

The Document aggregate carries **no timestamp** — that stayed true through the DDD refactor ([REN-44](https://linear.app/renewthedoc/issue/REN-44), [REN-49](https://linear.app/renewthedoc/issue/REN-49)), and nothing here adds one.

## 4. Deletion, and the rule that protects the user

A delete replaces the file's content with a tombstone:

```json
{ "id": "...", "deleted": true, "modifiedAt": "...", "schemaVersion": 1 }
```

Tombstones are **retained indefinitely** — a few hundred bytes in a hidden folder, and any purge policy would be a second distributed-deletion problem.

> ### Absence never deletes local data. Only a tombstone does.

This is the load-bearing rule of the whole design. A device that finds an empty folder where its snapshot says ten files existed **re-uploads its local state** rather than mirroring the emptiness — because an empty folder is indistinguishable from a provider glitch, a revoked scope, or a cloud-side accident, and mirroring it would turn any of those into silent destruction of the user's real data.

Deletion also cannot be inferred from a missing file, because that loses the delete-versus-concurrent-edit race: A deletes, B (not yet synced) re-uploads its copy, A resurrects it. A tombstone is a *write*, so it competes on the same timestamp rule.

**Consequence, surfaced in the UI (§6):** deleting the Cloud Backup on one device does not stop another connected device re-uploading it.

## 5. Change detection and local sync state

**Full folder listing with metadata on every sync**, behind the provider seam's change probe. Drive's `changes.list` + `startPageToken` adds persistent state that expires and needs a full-resync fallback anyway — building both paths to save one request against a folder of tens of files.

**Sync keeps its own tables**, in the same SQLite database, created by its own initializer (the connection holder allows a second context to register tables — [REN-48](https://linear.app/renewthedoc/issue/REN-48)):

- keyed by `DocumentId` / `OwnerId`
- **content hash of the last-synced payload** — this is how a local edit is detected at all, since the domain has no timestamp
- **last-seen provider `modifiedTime`**

No `ModifiedAt` column on the document row, no domain events, no journal written by the Documents repository — all three were rejected for inverting the dependency direction or leaking sync into the aggregate ([REN-49](https://linear.app/renewthedoc/issue/REN-49)).

## 6. Connection model

**Connecting is found, never offered.** A settings screen reached from the document list — no first-launch onboarding, no prompt after the first Document. Asking for Google consent before the user owns a single Document contradicts "nothing connected is first-class" at the loudest possible moment. Discoverability is the accepted cost.

The screen shows: connection state, connected account label, **last synced** (the honest signal — there is no background sync), count of malformed remote files skipped on the last run, **Disconnect**, and **Delete Cloud Backup**.

**Second device joins by union of ids**, silently. Guids do not collide, so nothing is lost. Honest downside: someone who hand-entered the same passport on both phones gets two identical-looking Documents — no dedupe heuristic is safe, since the same name and expiry could be two genuine renewals. Deleting one is recoverable; adopting the cloud copy would not be.

**Disconnect keeps everything**: local Documents stay, remote files are untouched, tokens are cleared. Those are the user's own files in their own cloud.

**Delete Cloud Backup** is separate, permanent (appdata files cannot be trashed — `files.delete` only), and **must run before revoking**, since a revoked token can delete nothing. Its confirmation must state that other connected devices will upload again.

**A broken connection is reported, not nagged.** A non-blocking banner appears on the list only when re-auth is genuinely needed (refresh token expired, access revoked from the account page). Transient offline stays silent and retries. Reminders keep firing locally throughout.

**Auth and storage:**

- Tokens via **platform-native sign-in** — Play Services / Credential Manager on Android, GoogleSignIn on iOS. `Google.Apis.Drive.v3` is not used, and `GoogleWebAuthorizationBroker` throws on mobile.
- **Refresh token and account label both in `SecureStorage`** (Keychain / Android Keystore). The email is a personal identifier and does not belong in plain preferences.
- **A restored device is treated as not connected** — keystore-bound entries do not survive a restore, so the credential really is gone. It then re-connects and merges by union.
- **OAuth client IDs are committed as ordinary config.** Installed-app clients hold no secret in the native/PKCE flow and the ID ships in the binary regardless; the protections are redirect/package-signature binding and PKCE. Exposure: someone reusing the ID could burn the Cloud project's quota — annoying, not dangerous.

## 7. Provider seam

**Storage only**, receiving an already-valid access token. Auth is deliberately outside it, because providers diverge most there and auth carries UI and lifecycle.

| Operation | Notes |
| --- | --- |
| list | per-file modified time **and** provider revision |
| read one | |
| write one | |
| delete one | a real delete, not a tombstone write |
| change probe | "what changed since X" |

The probe stays in the contract even though Drive's implementation is a poll: a Graph delta or Dropbox longpoll fits behind it, and omitting it is what would make a second provider expensive. The contract must not mirror Drive's API shape.

**One provider at launch.** A second gets built only when **users ask** or **Google removes the custom URI scheme** (deprecated, viable today via a per-client toggle; its removal would break installed users remotely).

## 8. Triggers, failure, and reminders

Sync runs on **app foreground**, **after a local save** (debounced), and **pull-to-refresh**. **No background or periodic sync** — iOS background execution is unreliable enough that promising it would be a lie in the product copy.

**Per-file, independent, best-effort.** One failure does not abort the batch; it retries next run. There is no transaction on a filesystem to roll back to.

**Forward compatibility:** unknown fields are **round-tripped**, and the app refuses to *write* a file whose `schemaVersion` exceeds what it understands while still reading what it can. This prevents an older app silently stripping a field a newer one added — data loss with no error anywhere.

**Malformed files are skipped and counted, never moved or deleted.** They are the user's own files, and the local copy might be the stale one. Sync validates at its own boundary; it must never rely on repository reconstitution to catch bad data, since reconstitution fails loud and one bad file would then break list loading ([REN-48](https://linear.app/renewthedoc/issue/REN-48)).

**After a pull, only the Documents that changed are re-planned**, through the existing application service. Re-planning everything would burn the iOS 64-pending-notification budget; leaving it to the startup pass alone ([REN-63](https://linear.app/renewthedoc/issue/REN-63)) would let a Document whose expiry changed elsewhere keep firing its old reminder until next launch.

## 9. Pre-flight gate for the build effort

**`appDataFolder` scoping is established by entailment, not by an explicit Google statement.** Google has never written the sentence; the verdict (per Cloud **project**, so Android and iOS share the folder) rests on three converging primary sources — decisively, that Play Games Saved Games *is* the Application Data Folder and Google's own setup page instructs developers to register two Android client IDs.

**Confirm empirically before building on it.** ~35 minutes, $0:

1. One Cloud project, two **Desktop** OAuth client IDs.
2. Authorize both against the same Google account from a laptop; scope `drive.appdata`.
3. Write a file to `appDataFolder` with client A; list `appDataFolder` with client B.

The shortcut is representative because token identity is `(client ID, user, scopes)` and **client type is not a token field**. It cannot exercise the case where MAUI's Android token path goes through Play Services rather than plain PKCE.

**Avoid the confound:** project-scoped consent may authorize client B *silently*. Silent consent proves consent sharing, not data sharing — the test passes only if **client B lists the file client A wrote**.

Full findings: `docs/research/byoc-storage.md`, Appendix A — currently on the unmerged branch `research/drive-appdata-scoping` (commit `77897a7`), not on `main`.

## 10. Web client: no-go

**Feasible, deliberately not built.** Google publishes a browser-JavaScript Drive quickstart, so a WASM client could reach Drive directly with no broker, and per-project scoping means a Web client would share the same folder.

Declined because the mobile app has shipped to nobody, and a browser cannot schedule the local notification that is the app's entire purpose.

**The catch that shapes any future attempt:** browser flows get **no refresh token** — Google's OAuth endpoint has no CORS, so the token comes from Google's hosted flow, the client re-prompts roughly hourly, and the token lives in JS memory. Acceptable for a desk-bound viewer; disqualifying for a sync engine.

**Recorded shape, for the day it reopens:** standalone Blazor WebAssembly on static hosting; its own "Web application" OAuth client in the same Cloud project, with the hosting domain in Authorized JavaScript origins; **viewer/editor only, never the sync engine**; no reminders, said plainly in the UI.

**Reopen triggers:** the mobile app ships and users ask for desktop entry, or a bulk-entry/import need the phone cannot serve.

## 11. README rewrite guidance

**Applies when network actually ships — not before.** Today's claims are literally true (the manifest has no INTERNET permission), so rewriting now would trade a stale claim for a false one.

**Lead:** *"Never miss a renewal. Your documents, in your own cloud — or on your device alone."* Reliability leads, privacy differentiates, and device-only reads as a first-class choice.

**"Privacy-first" as a label is retired**, replaced by concrete claims: reminders scheduled locally; no account (connecting is authorization you grant and revoke); the app asks only for its own private folder, not your drive; **your provider holds those files and can read them, we never can**; nothing connected is fully supported; network used solely to reach the cloud you connected; no analytics.

The provider disclosure sits **beside** the privacy claim, not in a separate section further down — a sceptical reader will otherwise catch us on it.

**Say plainly that the old claim was stronger in kind.** "No INTERNET permission" was *verifiable* from the manifest by anyone; "network only reaches your cloud" is a *promise*. Do not imply an even trade.

**Copy says "connect your cloud" (verb); the glossary keeps "Cloud Connection" (noun).** Deliberate divergence — do not reconcile.

**Claim inventory:** `README.md:3` (lead), `README.md:10` (claims bullet), `README.md:12` (planned-later list, partly superseded by sync), and the `AndroidManifest.xml` comment all change together when the permission is added. `CLAUDE.md` was updated during the map, since it instructs agents rather than users.

## 12. Still open (deliberately)

- **Offline/error UX specifics** — how sync failures and first-sync are surfaced beyond the state and count on the settings screen.
- **Provider quota and rate-limit behaviour in practice** — what the app does when it hits one.

Both are UX-shaped and belong to the build effort.

---

## Traceability

| Ticket | Decision |
| --- | --- |
| [REN-30](https://linear.app/renewthedoc/issue/REN-30) | Research: backend platforms (informed a path later ruled out) |
| [REN-31](https://linear.app/renewthedoc/issue/REN-31) | Research: web client options |
| [REN-39](https://linear.app/renewthedoc/issue/REN-39) | Research: bring-your-own-cloud storage providers |
| [REN-40](https://linear.app/renewthedoc/issue/REN-40) | Storage architecture — bring-your-own-cloud, no backend |
| [REN-32](https://linear.app/renewthedoc/issue/REN-32) | Positioning and the claim inventory |
| [REN-41](https://linear.app/renewthedoc/issue/REN-41) | Spike: `appDataFolder` scoping, consent publishing, URI scheme, disconnect deletion |
| [REN-42](https://linear.app/renewthedoc/issue/REN-42) | Provider choice, provider seam, hand-rolled REST |
| [REN-34](https://linear.app/renewthedoc/issue/REN-34) | Sync protocol |
| [REN-33](https://linear.app/renewthedoc/issue/REN-33) | Connection and authorization model |
| [REN-35](https://linear.app/renewthedoc/issue/REN-35) | Web client no-go and recorded shape |
| [REN-36](https://linear.app/renewthedoc/issue/REN-36), [REN-38](https://linear.app/renewthedoc/issue/REN-38) | Canceled unresolved — accounts and backend platform, mooted by REN-40 |
