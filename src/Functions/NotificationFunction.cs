using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using JbNet.Domain.Events;
using JbNet.Domain.Repositories;
using JbNet.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JbNet.Functions;

/// <summary>
/// Triggered by SQS (backed by an EventBridge rule → SQS fan-out).
/// Reads domain events and dispatches push notifications to registered devices via SNS.
/// </summary>
public sealed class NotificationFunction
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initialises DI for the notification Lambda.
    /// </summary>
    public NotificationFunction()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddInfrastructureServices(configuration);

        _services = services.BuildServiceProvider();
    }

    /// <summary>
    /// Lambda entry point — processes each SQS record independently.
    /// Re-throws on failure so SQS can retry / route to DLQ after max receives.
    /// </summary>
    public async Task Handler(SQSEvent sqsEvent, ILambdaContext context)
    {
        using var scope = _services.CreateScope();
        var logger      = scope.ServiceProvider.GetRequiredService<ILogger<NotificationFunction>>();
        var sns         = scope.ServiceProvider.GetRequiredService<IAmazonSimpleNotificationService>();
        var tokenRepo   = scope.ServiceProvider.GetRequiredService<IDeviceTokenRepository>();

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessMessageAsync(record.Body, sns, tokenRepo, logger, context.RemainingTime);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process SQS message {MessageId}", record.MessageId);
                throw; // let SQS retry → DLQ
            }
        }
    }

    private static async Task ProcessMessageAsync(
        string body,
        IAmazonSimpleNotificationService sns,
        IDeviceTokenRepository tokenRepo,
        ILogger logger,
        TimeSpan remaining)
    {
        // EventBridge → SQS wraps the event detail in a standard EventBridge event envelope
        // Body shape: { "detail-type": "...", "detail": { ... } }
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var detailType = root.TryGetProperty("detail-type", out var dt) ? dt.GetString() : null;
        var detail     = root.TryGetProperty("detail",      out var d)  ? d : default;

        logger.LogInformation("NotificationFunction: processing {EventType}", detailType);

        var message = detailType switch
        {
            nameof(ReferralRequestCreatedEvent)    => BuildCreatedMessage(detail),
            nameof(ReferralRequestForwardedEvent)  => BuildForwardedMessage(detail),
            nameof(ReferralRequestAcceptedEvent)   => BuildAcceptedMessage(detail),
            nameof(ReferralRequestDeclinedEvent)   => null, // decline is private — no notification
            nameof(ReferralRequestExpiredEvent)    => BuildExpiredMessage(detail),
            nameof(ReferralRequestWithdrawnEvent)  => null, // seeker withdrew — no notification needed
            _                                      => null
        };

        if (message is null) return;

        // Resolve all registered device endpoint ARNs for the recipient
        var endpointArns = await tokenRepo.GetEndpointArnsAsync(message.RecipientUserId);
        if (endpointArns.Count == 0)
        {
            logger.LogInformation(
                "NotificationFunction: no devices registered for user {UserId} — skipping push",
                message.RecipientUserId);
            return;
        }

        // Publish to each registered endpoint concurrently
        await Task.WhenAll(endpointArns.Select(arn => PublishToEndpointAsync(sns, arn, message, logger)));
    }

    private static async Task PublishToEndpointAsync(
        IAmazonSimpleNotificationService sns,
        string endpointArn,
        NotificationMessage message,
        ILogger logger)
    {
        try
        {
            await sns.PublishAsync(new PublishRequest
            {
                TargetArn = endpointArn,
                Subject   = message.Subject,
                Message   = message.Body
            });
            logger.LogInformation(
                "NotificationFunction: pushed to {Arn} — {Subject}", endpointArn, message.Subject);
        }
        catch (EndpointDisabledException)
        {
            // Stale or revoked device token — log and continue; token cleanup is a separate maintenance job
            logger.LogWarning("NotificationFunction: endpoint {Arn} disabled — token may be stale", endpointArn);
        }
    }

    private sealed record NotificationMessage(string RecipientUserId, string Subject, string Body);

    private static NotificationMessage? BuildCreatedMessage(JsonElement detail)
    {
        var firstHopId = detail.TryGetProperty("FirstHopParticipantId", out var h)  ? h.GetString()  : null;
        var jobTitle   = detail.TryGetProperty("JobTitle",              out var jt) ? jt.GetString() : "a role";
        if (string.IsNullOrEmpty(firstHopId)) return null;
        return new NotificationMessage(firstHopId,
            "Someone wants a referral",
            $"A connection is requesting a referral for {jobTitle}. Tap to review.");
    }

    private static NotificationMessage? BuildForwardedMessage(JsonElement detail)
    {
        var nextId = detail.TryGetProperty("NextParticipantId", out var np) ? np.GetString() : null;
        if (string.IsNullOrEmpty(nextId)) return null;
        return new NotificationMessage(nextId,
            "Referral request forwarded to you",
            "A referral request has reached you. Tap to review and accept or decline.");
    }

    private static NotificationMessage? BuildAcceptedMessage(JsonElement detail)
    {
        var seekerId = detail.TryGetProperty("JobSeekerId",  out var s) ? s.GetString() : null;
        var company  = detail.TryGetProperty("CompanyName",  out var c) ? c.GetString() : "the company";
        if (string.IsNullOrEmpty(seekerId)) return null;
        return new NotificationMessage(seekerId,
            "Referral accepted!",
            $"Great news! Someone at {company} has agreed to refer you internally.");
    }

    private static NotificationMessage? BuildExpiredMessage(JsonElement detail)
    {
        var seekerId = detail.TryGetProperty("JobSeekerId", out var s) ? s.GetString() : null;
        if (string.IsNullOrEmpty(seekerId)) return null;
        return new NotificationMessage(seekerId,
            "Referral request expired",
            "Your referral request has expired after 7 days. You can try again later.");
    }
}
