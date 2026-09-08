using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Application.Tests.Documents;

/// <summary>
/// One shared log across all three collaborators, so tests can assert the *interleaved* call order
/// — which is the whole point of a characterization test over a two-collaborator sequence.
/// </summary>
public sealed class CallLog
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    public void Record(string call) => _calls.Add(call);
}

public sealed class FakeDocumentStore : IDocumentStore
{
    private readonly CallLog _log;
    private readonly List<Document> _documents;

    public FakeDocumentStore(CallLog log, params Document[] seed)
    {
        _log = log;
        _documents = [.. seed];
    }

    public List<Document> Added { get; } = [];
    public List<Document> Updated { get; } = [];
    public List<DocumentId> Deleted { get; } = [];

    public Task<IReadOnlyList<Document>> GetAllAsync(CancellationToken ct = default)
    {
        _log.Record("documents.GetAll");
        return Task.FromResult<IReadOnlyList<Document>>(_documents.ToList());
    }

    public Task AddAsync(Document document, CancellationToken ct = default)
    {
        _log.Record($"documents.Add({document.Name})");
        Added.Add(document);
        _documents.Add(document);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Document document, CancellationToken ct = default)
    {
        _log.Record($"documents.Update({document.Name})");
        Updated.Add(document);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(DocumentId documentId, CancellationToken ct = default)
    {
        _log.Record($"documents.Delete({documentId})");
        Deleted.Add(documentId);
        return Task.CompletedTask;
    }
}

public sealed class FakeOwnerStore : IOwnerStore
{
    private readonly CallLog _log;
    private readonly List<Owner> _owners;

    public FakeOwnerStore(CallLog log, params Owner[] seed)
    {
        _log = log;
        _owners = [.. seed];
    }

    public Owner? LastAdded { get; private set; }

    public Task<IReadOnlyList<Owner>> GetAllAsync(CancellationToken ct = default)
    {
        _log.Record("owners.GetAll");
        return Task.FromResult<IReadOnlyList<Owner>>(_owners.ToList());
    }

    public Task AddAsync(Owner owner, CancellationToken ct = default)
    {
        _log.Record($"owners.Add({owner.Name})");
        LastAdded = owner;
        _owners.Add(owner);
        return Task.CompletedTask;
    }
}

public sealed class FakeReminderScheduler : IReminderScheduler
{
    private readonly CallLog _log;

    public FakeReminderScheduler(CallLog log) => _log = log;

    public List<Document> Scheduled { get; } = [];
    public List<DocumentId> Cancelled { get; } = [];

    public Task ScheduleAsync(Document document, CancellationToken ct = default)
    {
        _log.Record($"scheduler.Schedule({document.Name})");
        Scheduled.Add(document);
        return Task.CompletedTask;
    }

    public Task CancelAsync(DocumentId documentId, CancellationToken ct = default)
    {
        _log.Record($"scheduler.Cancel({documentId})");
        Cancelled.Add(documentId);
        return Task.CompletedTask;
    }

    public Task EnsurePermissionAsync(CancellationToken ct = default)
    {
        _log.Record("scheduler.EnsurePermission");
        return Task.CompletedTask;
    }
}
