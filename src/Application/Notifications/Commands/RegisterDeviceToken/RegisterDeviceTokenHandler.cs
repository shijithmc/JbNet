using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using JbNet.Domain.Aggregates.Notifications;
using JbNet.Domain.Repositories;
using MediatR;

namespace JbNet.Application.Notifications.Commands.RegisterDeviceToken;

/// <summary>
/// Handles <see cref="RegisterDeviceTokenCommand"/>.
/// Creates (or updates) an SNS Platform Endpoint for the token, then persists the mapping.
/// </summary>
public sealed class RegisterDeviceTokenHandler : IRequestHandler<RegisterDeviceTokenCommand, string>
{
    private readonly IDeviceTokenRepository _tokenRepository;
    private readonly IAmazonSimpleNotificationService _sns;

    /// <summary>
    /// Initialises the handler.
    /// </summary>
    public RegisterDeviceTokenHandler(
        IDeviceTokenRepository tokenRepository,
        IAmazonSimpleNotificationService sns)
    {
        _tokenRepository = tokenRepository;
        _sns             = sns;
    }

    /// <inheritdoc />
    public async Task<string> Handle(RegisterDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        // Create or retrieve the SNS Platform Endpoint for this token.
        // CreatePlatformEndpoint is idempotent — calling it with the same token returns the existing ARN.
        var endpointResponse = await _sns.CreatePlatformEndpointAsync(
            new CreatePlatformEndpointRequest
            {
                PlatformApplicationArn = request.SnsApplicationArn,
                Token                  = request.RawToken,
                CustomUserData         = request.UserId
            }, cancellationToken);

        var endpointArn = endpointResponse.EndpointArn;
        var tokenId     = Guid.NewGuid().ToString();

        var token = DeviceToken.Create(
            tokenId,
            request.UserId,
            request.RawToken,
            endpointArn,
            request.Platform,
            DateTimeOffset.UtcNow);

        await _tokenRepository.SaveAsync(token, cancellationToken);

        return endpointArn;
    }
}
