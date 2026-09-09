using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Persistence.Notifications;

/// <summary>
/// The notification number the app derived from a <see cref="DocumentId"/> before REN-54: a
/// <see cref="Guid.GetHashCode"/> folded into a non-negative int.
/// </summary>
/// <remarks>
/// <para>
/// Lossy by construction — 128 bits into 31 — so two Documents can share a number, and then one's
/// cancel silently kills the other's Reminder. <c>Guid.GetHashCode</c> XORs the Guid's four 32-bit
/// words, which is deterministic across runs and platforms, so a collision is permanent rather than
/// intermittent: nothing the user does clears it.
/// </para>
/// <para>
/// Extracted verbatim from <c>LocalNotificationReminderScheduler</c> so the mapping it is about to
/// stop using is pinned by a test first (spec §4.1 names this bug and leaves it to REN-54).
/// </para>
/// </remarks>
public static class DerivedNotificationNumber
{
    /// <summary>The number this Document's notification used to be scheduled under.</summary>
    public static int For(DocumentId documentId) => documentId.Value.GetHashCode() & 0x7FFFFFFF;
}
