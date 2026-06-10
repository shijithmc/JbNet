using JbNet.Domain.Aggregates.Notifications;

namespace JbNet.Domain.Repositories;

/// <summary>
/// Manages device push-notification token records per user.
/// </summary>
public interface IDeviceTokenRepository
{
    /// <summary>Returns all active SNS endpoint ARNs registered for a user.</summary>
    Task<IReadOnlyList<string>> GetEndpointArnsAsync(string userId, CancellationToken ct = default);

    /// <summary>Persists a new device token record, overwriting any existing record for the same raw token.</summary>
    Task SaveAsync(DeviceToken token, CancellationToken ct = default);

    /// <summary>Removes a device token record by id.</summary>
    Task DeleteAsync(string userId, string tokenId, CancellationToken ct = default);
}
