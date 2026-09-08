using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Application.Documents;

/// <summary>
/// Use cases over the Document aggregate: add, edit, delete, list, and the notification-permission
/// prompt. The call sequences here are a verbatim copy of the page code-behind they replaced —
/// quirks included, deliberately (see docs/architecture/ddd-refactor.md §8 steps 2–3).
/// </summary>
public sealed class DocumentAppService
{
    private readonly IDocumentRepository _documents;
    private readonly IReminderScheduler _scheduler;

    public DocumentAppService(IDocumentRepository documents, IReminderScheduler scheduler)
    {
        _documents = documents;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Documents grouped by state, expired first then by nearest expiry. The owner filter is a
    /// single parameter: no <paramref name="ownerFilter"/> means every owner's documents, Me means
    /// the user's own, and a Person means that one person's (spec §4).
    /// </summary>
    public async Task<IReadOnlyList<DocumentGroup>> ListAsync(
        DocumentOwner? ownerFilter, DocumentState? statusFilter, DateOnly today)
    {
        var documents = (await _documents.GetAllAsync()).AsEnumerable();
        if (ownerFilter is { } owner)
            documents = documents.Where(d => d.Owner == owner);
        if (statusFilter is { } state)
            documents = documents.Where(d => d.StateOn(today) == state);

        return DocumentList.Grouped(documents, today);
    }

    // Add and edit write through the same upsert; what still distinguishes them is the reminder
    // orchestration, which is the app service's job (spec §5.1).
    public async Task AddAsync(Document document, DateTime nowLocal)
    {
        await _documents.SaveAsync(document);
        await ScheduleReminderAsync(document, nowLocal);
    }

    public async Task EditAsync(Document document, DateTime nowLocal)
    {
        await _documents.SaveAsync(document);
        await _scheduler.CancelAsync(document.Id); // edit = re-creation (CONTEXT.md)
        await ScheduleReminderAsync(document, nowLocal);
    }

    public async Task DeleteAsync(DocumentId documentId)
    {
        await _scheduler.CancelAsync(documentId);
        await _documents.RemoveAsync(documentId);
    }

    public Task<bool> EnsureNotificationPermissionAsync() => _scheduler.EnsurePermissionAsync();

    /// <summary>
    /// The seam the whole refactor exists for: the aggregate decides whether and when the Reminder
    /// fires, and the scheduler is handed that decision plus the text inputs. Time arrives as an
    /// argument, so neither the domain nor the adapter reads a clock (spec §4.1).
    /// </summary>
    private Task ScheduleReminderAsync(Document document, DateTime nowLocal) =>
        _scheduler.ScheduleAsync(
            document.Id, document.PlanReminder(nowLocal), ReminderContent.Of(document));
}
