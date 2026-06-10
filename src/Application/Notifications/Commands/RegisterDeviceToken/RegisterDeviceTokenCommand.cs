using MediatR;

namespace JbNet.Application.Notifications.Commands.RegisterDeviceToken;

/// <summary>
/// Registers or refreshes a device push-notification token for the calling user.
/// Creates an SNS Platform Endpoint for the token so the notification Lambda can
/// resolve the endpoint ARN per recipient.
/// </summary>
/// <param name="UserId">Authenticated user registering the device.</param>
/// <param name="RawToken">Platform-specific device token from FCM or APNs.</param>
/// <param name="Platform">Device platform: "FCM" (Android) or "APNS" (iOS).</param>
/// <param name="SnsApplicationArn">SNS Platform Application ARN for the target platform.</param>
public sealed record RegisterDeviceTokenCommand(
    string UserId,
    string RawToken,
    string Platform,
    string SnsApplicationArn) : IRequest<string>;
