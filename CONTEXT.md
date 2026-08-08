# RenewTheDoc — Ubiquitous Language

## Document

A thing the user wants to renew before it stops being valid — an ID, passport, insurance policy, registration. Has a **Name**, an **Expiry Date**, a **Remind-Before**, and an optional free-text **Note**. No dedicated document-number field; the Note holds anything extra.

## Expiry Date

The calendar date (no time of day, interpreted in the device's local timezone) on which a Document stops being valid.

## Remind-Before

The per-Document lead time before the Expiry Date at which the user wants to be reminded. Chosen from presets (1 week, 1 month, 3 months) or a custom number of days. Exactly one Remind-Before per Document.

Remind-Before does double duty: it triggers the Reminder, and it defines the Document's Expiring Soon window.

## Reminder

The single scheduled local notification for a Document. Fires at 09:00 local time on the date (Expiry Date − Remind-Before). Its text names the Document and its Expiry Date, in the app's language.

- If the Reminder moment is already in the past when the Document is added (but the Document is not yet expired), the Reminder fires immediately, once.
- An already-expired Document gets no Reminder — its Expired state in the list is the signal.
- Tapping a Reminder opens the app on the Document list.

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
