using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Application.Documents;

/// <summary>
/// Use cases over the Document aggregate: add, edit, delete, list, and the notification-permission
/// prompt. The call sequences here are a verbatim copy of the page code-behind they replaced —
/// quirks included, deliberately (see docs/architecture/ddd-refactor.md §8 steps 2–3).
/// </summary>
public sealed class DocumentAppService
{
    private readonly IDocumentStore _documents;
    private readonly IReminderScheduler _scheduler;

    public DocumentAppService(IDocumentStore documents, IReminderScheduler scheduler)
    {
        _documents = documents;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Documents grouped by state, expired first then by nearest expiry. When
    /// <paramref name="ownerFilterActive"/> is false the owner filter is "All"; when it is true a
    /// null <paramref name="ownerId"/> means "Me" (documents without an owner).
    /// </summary>
    public async Task<IReadOnlyList<DocumentGroup>> ListAsync(
        bool ownerFilterActive, OwnerId? ownerId, DocumentState? statusFilter, DateOnly today)
    {
        var documents = (await _documents.GetAllAsync()).AsEnumerable();
        if (ownerFilterActive)
            documents = documents.Where(d => d.OwnerId == ownerId);
        if (statusFilter is { } state)
            documents = documents.Where(d => d.StateOn(today) == state);

        return DocumentList.Grouped(documents, today);
    }

    public async Task AddAsync(Document document)
    {
        await _documents.AddAsync(document);
        await _scheduler.ScheduleAsync(document);
    }

    public async Task EditAsync(Document document)
    {
        await _documents.UpdateAsync(document);
        await _scheduler.CancelAsync(document.Id); // edit = re-creation (CONTEXT.md)
        await _scheduler.ScheduleAsync(document);
    }

    public async Task DeleteAsync(DocumentId documentId)
    {
        await _scheduler.CancelAsync(documentId);
        await _documents.DeleteAsync(documentId);
    }

    public Task EnsureNotificationPermissionAsync() => _scheduler.EnsurePermissionAsync();
}
