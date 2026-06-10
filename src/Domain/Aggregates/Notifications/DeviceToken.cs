namespace JbNet.Domain.Aggregates.Notifications;

/// <summary>
/// Represents a registered device push-notification endpoint for a user.
/// One user may have multiple devices (phone + tablet). Each record maps a
/// platform-specific FCM/APNs token to an SNS Platform Endpoint ARN.
/// </summary>
public sealed class DeviceToken
{
    /// <summary>Unique identifier for this device registration record.</summary>
    public string TokenId { get; private set; }

    /// <summary>The user who owns this device.</summary>
    public string UserId { get; private set; }

    /// <summary>Raw device token from FCM (Android) or APNs (iOS).</summary>
    public string RawToken { get; private set; }

    /// <summary>AWS SNS Platform Application ARN endpoint created for this token.</summary>
    public string SnsEndpointArn { get; private set; }

    /// <summary>Device platform identifier.</summary>
    public string Platform { get; private set; }

    /// <summary>UTC timestamp when the token was registered.</summary>
    public DateTimeOffset RegisteredAt { get; private set; }

    private DeviceToken() { TokenId = ""; UserId = ""; RawToken = ""; SnsEndpointArn = ""; Platform = ""; }

    /// <summary>
    /// Creates a new device token registration record.
    /// </summary>
    /// <param name="tokenId">Unique id (caller-supplied GUID).</param>
    /// <param name="userId">Owning user id.</param>
    /// <param name="rawToken">Device token string from FCM/APNs.</param>
    /// <param name="snsEndpointArn">SNS endpoint ARN created for this token.</param>
    /// <param name="platform">Device platform ("FCM" or "APNS").</param>
    /// <param name="registeredAt">Registration timestamp (UTC).</param>
    public static DeviceToken Create(
        string tokenId,
        string userId,
        string rawToken,
        string snsEndpointArn,
        string platform,
        DateTimeOffset registeredAt)
    {
        if (string.IsNullOrWhiteSpace(tokenId))    throw new ArgumentException("TokenId required.",      nameof(tokenId));
        if (string.IsNullOrWhiteSpace(userId))      throw new ArgumentException("UserId required.",       nameof(userId));
        if (string.IsNullOrWhiteSpace(rawToken))    throw new ArgumentException("RawToken required.",     nameof(rawToken));
        if (string.IsNullOrWhiteSpace(snsEndpointArn)) throw new ArgumentException("SnsEndpointArn required.", nameof(snsEndpointArn));
        if (string.IsNullOrWhiteSpace(platform))    throw new ArgumentException("Platform required.",    nameof(platform));

        return new DeviceToken
        {
            TokenId        = tokenId,
            UserId         = userId,
            RawToken       = rawToken,
            SnsEndpointArn = snsEndpointArn,
            Platform       = platform,
            RegisteredAt   = registeredAt
        };
    }
}
