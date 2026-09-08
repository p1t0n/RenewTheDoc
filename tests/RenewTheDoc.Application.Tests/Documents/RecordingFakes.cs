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

public sealed class FakeDocumentRepository : IDocumentRepository
{
    private readonly CallLog _log;
    private readonly List<Document> _documents;

    public FakeDocumentRepository(CallLog log, params Document[] seed)
    {
        _log = log;
        _documents = [.. seed];
    }

    /// <summary>Every upsert, in order — add and edit are the same call now.</summary>
    public List<Document> Saved { get; } = [];
    public List<DocumentId> Removed { get; } = [];

    public Task<Document?> GetAsync(DocumentId id)
    {
        _log.Record($"documents.Get({id})");
        return Task.FromResult(_documents.SingleOrDefault(d => d.Id == id));
    }

    public Task<IReadOnlyList<Document>> GetAllAsync()
    {
        _log.Record("documents.GetAll");
        return Task.FromResult<IReadOnlyList<Document>>(_documents.ToList());
    }

    public Task SaveAsync(Document document)
    {
        _log.Record($"documents.Save({document.Name})");
        Saved.Add(document);
        _documents.RemoveAll(d => d.Id == document.Id);
        _documents.Add(document);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(DocumentId id)
    {
        _log.Record($"documents.Remove({id})");
        Removed.Add(id);
        _documents.RemoveAll(d => d.Id == id);
        return Task.CompletedTask;
    }
}

public sealed class FakeOwnerRepository : IOwnerRepository
{
    private readonly CallLog _log;
    private readonly List<Owner> _owners;

    public FakeOwnerRepository(CallLog log, params Owner[] seed)
    {
        _log = log;
        _owners = [.. seed];
    }

    public Owner? LastSaved { get; private set; }

    public Task<Owner?> GetAsync(OwnerId id)
    {
        _log.Record($"owners.Get({id})");
        return Task.FromResult(_owners.SingleOrDefault(o => o.Id == id));
    }

    public Task<IReadOnlyList<Owner>> GetAllAsync()
    {
        _log.Record("owners.GetAll");
        return Task.FromResult<IReadOnlyList<Owner>>(_owners.ToList());
    }

    public Task SaveAsync(Owner owner)
    {
        _log.Record($"owners.Save({owner.Name})");
        LastSaved = owner;
        _owners.RemoveAll(o => o.Id == owner.Id);
        _owners.Add(owner);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(OwnerId id)
    {
        _log.Record($"owners.Remove({id})");
        _owners.RemoveAll(o => o.Id == id);
        return Task.CompletedTask;
    }
}

public sealed class FakeReminderScheduler : IReminderScheduler
{
    private readonly CallLog _log;

    public FakeReminderScheduler(CallLog log) => _log = log;

    public List<Document> Scheduled { get; } = [];
    public List<DocumentId> Cancelled { get; } = [];

    public Task ScheduleAsync(Document document)
    {
        _log.Record($"scheduler.Schedule({document.Name})");
        Scheduled.Add(document);
        return Task.CompletedTask;
    }

    public Task CancelAsync(DocumentId documentId)
    {
        _log.Record($"scheduler.Cancel({documentId})");
        Cancelled.Add(documentId);
        return Task.CompletedTask;
    }

    public Task EnsurePermissionAsync()
    {
        _log.Record("scheduler.EnsurePermission");
        return Task.CompletedTask;
    }
}
