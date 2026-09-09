---
status: accepted
date: 2026-09-09
---

# One file per Document, last-write-wins on the provider's clock, deletes as tombstones

Local SQLite stays the source of truth. Each Document and Owner mirrors as one small JSON file (`doc-{id}.json`, `owner-{id}.json`); conflicts resolve by **last-write-wins comparing the provider's `modifiedTime`**, and deletes are written as **tombstone files retained indefinitely**.

A combined blob was rejected because, with no conditional writes available, two devices editing anything concurrently would silently lose a whole round of edits. Per-file granularity confines a lost update to the one Document that was edited twice.

## Consequences

- **This is last-*upload*-wins, not last-*edit*-wins.** A phone that edited offline a week ago and syncs today overwrites yesterday's edit from another device. Device-written timestamps would give better semantics but fail far worse when a clock is wrong — silently, permanently, and invisibly. Foreground sync keeps the upload gap small.
- **Absence never deletes local data — only a tombstone does.** A device finding an empty folder where its snapshot says files existed **re-uploads** rather than mirroring the emptiness, because an empty folder is indistinguishable from a provider glitch or a revoked scope. The cost: deleting the Cloud Backup on one device does not stop another connected device restoring it, so the UI must say so.
- **Compare-and-swap is a rejected option, not an open one.** Reopening it needs a different provider *and* a different protocol.
- **The domain aggregate stays timestamp-free.** Local change detection is a content hash in sync's own tables — no `ModifiedAt` column, no domain events, no journal written by the Documents repository, all of which would leak sync into the domain or invert the dependency direction.
- **Unknown fields are round-tripped** and the app refuses to write a file whose `schemaVersion` it doesn't understand, so an older app cannot silently strip a field a newer one added.
- **Files stay plain, language-neutral JSON** — no .NET type discriminators — preserving the option of a browser client that reads them without a .NET-shaped parser.
