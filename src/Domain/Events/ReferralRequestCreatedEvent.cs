using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Events;

/// <summary>Raised when a job seeker creates a new referral request. Triggers notification to the 1st-hop intermediary.</summary>
public sealed record ReferralRequestCreatedEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    RequestId RequestId,
    UserId JobSeekerId,
    JobId JobId,
    UserId FirstHopUserId,
    string CompanyName,
    string JobTitle
) : IDomainEvent;
