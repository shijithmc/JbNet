namespace JbNet.Domain.Exceptions;

/// <summary>Base exception for domain rule violations. Translates to 422 Unprocessable Entity at the API layer.</summary>
public class DomainException(string message) : Exception(message);

public sealed class ActiveRequestLimitExceededException(int limit)
    : DomainException($"Cannot create referral request: active request limit of {limit} reached.");

public sealed class DuplicateRequestException(string jobId)
    : DomainException($"An active referral request for job '{jobId}' already exists.");

public sealed class CooldownActiveException(DateTimeOffset expiresAt)
    : DomainException($"Cooldown active until {expiresAt:O}. Cannot request referral for this job until then.");

public sealed class RequestNotActiveException(string requestId)
    : DomainException($"Referral request '{requestId}' is not in an active state.");

public sealed class UnauthorizedHopActionException(string userId, string requestId)
    : DomainException($"User '{userId}' is not the current pending participant for request '{requestId}'.");

public sealed class ConnectionLimitExceededException(int limit)
    : DomainException($"Connection limit of {limit} reached.");

public sealed class ConnectionAlreadyExistsException(string targetUserId)
    : DomainException($"A connection with user '{targetUserId}' already exists.");
