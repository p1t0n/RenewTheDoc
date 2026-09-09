# RenewTheDoc — Ubiquitous Language

## Cloud Connection

The optional, per-device link between the app and the user's own personal cloud storage, authorized by the user. While connected, Documents and Owners are mirrored into a folder the app owns inside that storage, so the user's other connected devices see the same data.

A Cloud Connection is authorization to use storage the user already has — never an identity the app issues, and never an account the app holds. The app keeps no copy of the data. A device with no Cloud Connection is fully functional: everything works locally.

A Cloud Connection is not an Owner. Owners remain labels on Documents and never sign in.

## Cloud Backup

The copy of the user's Documents and Owners kept in their own cloud storage while a Cloud Connection exists — one small file per Document and per Owner, inside a folder the app owns.

It mirrors the **current** state and nothing else: there is no version history and no restoring yesterday's copy. "Backup" is the word people already use for "my data is safe somewhere other than this phone", and in that sense it is accurate — the Documents survive losing the device — but it is never a promise of point-in-time recovery.

Deleting the Cloud Backup is explicit and permanent, and separate from disconnecting. A device that is still connected will upload its own copy again afterwards, so clearing the data everywhere means disconnecting each device too.

## Document

A thing the user wants to renew before it stops being valid — an ID, passport, insurance policy, registration. Has a **Name**, an **Expiry Date**, a **Remind-Before**, an optional free-text **Note**, and an optional **Country**. No dedicated document-number field; the Note holds anything extra.

## Owner

The person a Document belongs to — the user's relative or anyone else. Owners live in a small user-managed dictionary. Two Owners may share a name; each is still a distinct person.

A Document without an Owner belongs to the user, shown as a localized **Me**. Me is not a dictionary entry and not an identity the app issues: it is simply whoever is using the app, the same person on every device connected to their own cloud storage. Consequence, accepted: if two people ever connect the *same* cloud storage, each sees the other's unowned Documents as their own.

An Owner cannot be removed from the dictionary while any Document still names them — such a Document must be reassigned first. Removing an Owner never rewrites who a Document belongs to, and never deletes Documents.

## Country

The country a Document is issued in or valid for — optional, since not every document is country-bound. Stored as an ISO 3166-1 alpha-2 code; displayed with the localized country name. Future country-specific features (templates, online renewal) key on this code.

## Expiry Date

The calendar date (no time of day, interpreted in the device's local timezone) on which a Document stops being valid.

## Remind-Before

The per-Document lead time before the Expiry Date at which the user wants to be reminded. Chosen from presets (1 week, 1 month, 3 months) or a custom number of days. Exactly one Remind-Before per Document.

Remind-Before does double duty: it triggers the Reminder, and it defines the Document's Expiring Soon window.

## Reminder

The single standing request a Document makes to be told about its own expiry — once, at 09:00 local time on the date (Expiry Date − Remind-Before). The device honours it by alerting the user; the alert names the Document and its Expiry Date, in the app's language.

- If the Reminder moment is already in the past when the Document is added (but the Document is not yet expired), the Reminder fires immediately, once.
- An already-expired Document gets no Reminder — its Expired state in the list is the signal.
- Tapping a Reminder opens the app on the Document list.
- **Editing** a Document cancels its Reminder and plans a new one under exactly the creation rules above — an edit behaves like re-creation. **Deleting** a Document cancels its Reminder.

## Document states

Every Document is in exactly one state, derived from today's date:

| State | Meaning |
| --- | --- |
| **Expired** | Expiry Date is in the past |
| **Expiring Soon** | Today is within the Remind-Before window of the Expiry Date |
| **OK** | Everything else |

The list orders Expired first, then by nearest Expiry Date.

## Language

The app speaks English, Polish, and Russian. It follows the system locale, falling back to English. Reminder text and date formats follow the same locale. No in-app language picker.
