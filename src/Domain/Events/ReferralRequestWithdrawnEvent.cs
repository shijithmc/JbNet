using JbNet.Domain.ValueObjects;

namespace JbNet.Domain.Events;

/// <summary>Raised when the job seeker withdraws an active request. Triggers resume access revocation for all intermediaries.</summary>
public sealed record ReferralRequestWithdrawnEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    RequestId RequestId,
    UserId JobSeekerId,
    string ResumeS3Key
) : IDomainEvent;
