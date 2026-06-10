using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Events;

/// <summary>Raised by the expiry Lambda when a request exceeds 7 days without action at the current hop.</summary>
public sealed record ReferralRequestExpiredEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    RequestId RequestId,
    UserId JobSeekerId,
    UserId PendingParticipantId,
    string CompanyName,
    string JobTitle
) : IDomainEvent;
