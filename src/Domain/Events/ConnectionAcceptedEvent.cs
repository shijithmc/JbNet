using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Events;

/// <summary>Raised when a connection request is accepted. Triggers BFS cache invalidation for both users.</summary>
public sealed record ConnectionAcceptedEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    UserId RequesterId,
    UserId AccepterId
) : IDomainEvent;
