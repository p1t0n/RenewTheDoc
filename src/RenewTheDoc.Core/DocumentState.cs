namespace RenewTheDoc.Core;

public enum DocumentState
{
    Expired,
    ExpiringSoon,
    Ok,
}

public static class DocumentStateExtensions
{
    /// <summary>Derives the document's state from today's date. Expiry date itself is not yet expired.</summary>
    public static DocumentState GetState(this Document document, DateOnly today)
    {
        if (document.ExpiryDate < today) return DocumentState.Expired;
        var windowStart = document.ExpiryDate.AddDays(-document.RemindBefore.Days);
        return today >= windowStart ? DocumentState.ExpiringSoon : DocumentState.Ok;
    }
}
