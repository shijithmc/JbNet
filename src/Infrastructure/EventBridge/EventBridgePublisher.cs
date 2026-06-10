using System.Text.Json;
using Amazon.EventBridge;
using Amazon.EventBridge.Model;
using JbNet.Application.Common;
using JbNet.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JbNet.Infrastructure.EventBridge;

public sealed class EventBridgePublisher(
    IAmazonEventBridge eventBridge,
    IOptions<EventBridgeOptions> options,
    ILogger<EventBridgePublisher> logger) : IEventPublisher
{
    private readonly string _busName = options.Value.BusName;
    private readonly string _source = "jbnet.api";

    public Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct) =>
        PublishManyAsync([domainEvent], ct);

    public async Task PublishManyAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct)
    {
        if (events.Count == 0) return;

        var entries = events.Select(e => new PutEventsRequestEntry
        {
            EventBusName = _busName,
            Source = _source,
            DetailType = e.GetType().Name,
            Detail = JsonSerializer.Serialize(e, e.GetType()),
            Time = e.OccurredAt.UtcDateTime
        }).ToList();

        // EventBridge PutEvents: max 10 entries per call
        const int batchSize = 10;
        for (int i = 0; i < entries.Count; i += batchSize)
        {
            var batch = entries.Skip(i).Take(batchSize).ToList();
            var response = await eventBridge.PutEventsAsync(
                new PutEventsRequest { Entries = batch }, ct);

            if (response.FailedEntryCount > 0)
            {
                logger.LogError(
                    "EventBridge PutEvents: {FailCount}/{Total} entries failed",
                    response.FailedEntryCount, batch.Count);
            }
        }
    }
}

public sealed class EventBridgeOptions
{
    public const string SectionName = "EventBridge";
    public string BusName { get; set; } = "jbnet-bus";
}
