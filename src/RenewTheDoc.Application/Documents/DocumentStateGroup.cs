using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Application.Documents;

/// <summary>
/// A run of Documents sharing a state, in glossary order. Carries domain objects only — titles,
/// colours and day counts stay in the UI.
/// </summary>
public sealed record DocumentStateGroup(DocumentState State, IReadOnlyList<Document> Documents);
