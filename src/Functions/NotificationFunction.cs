using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using JbNet.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JbNet.Functions;

/// <summary>
/// Triggered by SQS (backed by an EventBridge rule → SNS → SQS fan-out).
/// Reads domain events and dispatches push notifications and/or emails to recipients.
/// </summary>
public sealed class NotificationFunction
{
    private readonly IServiceProvider _services;

    public NotificationFunction()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddSingleton<IAmazonSimpleNotificationService, AmazonSimpleNotificationServiceClient>();

        _services = services.BuildServiceProvider();
    }

    public async Task Handler(SQSEvent sqsEvent, ILambdaContext context)
    {
        var logger = _services.GetRequiredService<ILogger<NotificationFunction>>();
        var sns = _services.GetRequiredService<IAmazonSimpleNotificationService>();
        var platformArn = Environment.GetEnvironmentVariable("SNS_PLATFORM_ARN") ?? string.Empty;

        foreach (var record in sqsEvent.Records)
        {
            try
            {
                await ProcessMessageAsync(record.Body, sns, platformArn, logger, context.RemainingTime);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process SQS message {MessageId}", record.MessageId);
                // Let SQS retry by re-throwing — message goes back to queue or DLQ after max receives
                throw;
            }
        }
    }

    private static async Task ProcessMessageAsync(
        string body,
        IAmazonSimpleNotificationService sns,
        string platformArn,
        ILogger logger,
        TimeSpan remaining)
    {
        // EventBridge → SQS wraps the event detail inside an SNS notification body
        // Body shape: { "detail-type": "...", "detail": { ... } }
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var detailType = root.TryGetProperty("detail-type", out var dt) ? dt.GetString() : null;
        var detail = root.TryGetProperty("detail", out var d) ? d : default;

        logger.LogInformation("NotificationFunction: processing event type {EventType}", detailType);

        var message = detailType switch
        {
            nameof(ReferralRequestCreatedEvent) => BuildCreatedMessage(detail),
            nameof(ReferralRequestForwardedEvent) => BuildForwardedMessage(detail),
            nameof(ReferralRequestAcceptedEvent) => BuildAcceptedMessage(detail),
            nameof(ReferralRequestDeclinedEvent) => null, // decline is private — no notification to seeker
            nameof(ReferralRequestExpiredEvent) => BuildExpiredMessage(detail),
            nameof(ReferralRequestWithdrawnEvent) => null, // seeker withdrew — no notification needed
            _ => null
        };

        if (message is null || string.IsNullOrEmpty(platformArn)) return;

        // In production, resolve device endpoint ARN per recipient from a device-token store.
        // Placeholder: log and return — actual endpoint resolution requires a DeviceToken repository.
        logger.LogInformation(
            "NotificationFunction: would push to recipient {RecipientId} — {Subject}: {Body}",
            message.RecipientUserId, message.Subject, message.Body);

        // Uncomment when device token store is wired up:
        // await sns.PublishAsync(new PublishRequest
        // {
        //     TargetArn = resolvedEndpointArn,
        //     Subject = message.Subject,
        //     Message = message.Body
        // });
    }

    private sealed record NotificationMessage(string RecipientUserId, string Subject, string Body);

    private static NotificationMessage? BuildCreatedMessage(JsonElement detail)
    {
        var firstHopId = detail.TryGetProperty("FirstHopParticipantId", out var h) ? h.GetString() : null;
        var jobTitle = detail.TryGetProperty("JobTitle", out var jt) ? jt.GetString() : "a role";
        if (string.IsNullOrEmpty(firstHopId)) return null;
        return new NotificationMessage(firstHopId,
            "Someone wants a referral",
            $"A connection is requesting a referral for {jobTitle}. Tap to review.");
    }

    private static NotificationMessage? BuildForwardedMessage(JsonElement detail)
    {
        var nextParticipantId = detail.TryGetProperty("NextParticipantId", out var np) ? np.GetString() : null;
        if (string.IsNullOrEmpty(nextParticipantId)) return null;
        return new NotificationMessage(nextParticipantId,
            "Referral request forwarded to you",
            "A referral request has reached you. Tap to review and accept or decline.");
    }

    private static NotificationMessage? BuildAcceptedMessage(JsonElement detail)
    {
        var seekerId = detail.TryGetProperty("JobSeekerId", out var s) ? s.GetString() : null;
        var company = detail.TryGetProperty("CompanyName", out var c) ? c.GetString() : "the company";
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
