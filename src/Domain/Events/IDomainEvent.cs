namespace JbNet.Domain.Events;

/// <summary>Marker interface for all domain events. Events are raised by aggregates and published to EventBridge via the Application layer.</summary>
public interface IDomainEvent
{
    string EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
