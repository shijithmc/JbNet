using JbNet.Domain.Events;

namespace JbNet.Application.Common;

/// <summary>Abstraction for publishing domain events to EventBridge. Implemented in Infrastructure.</summary>
public interface IEventPublisher
{
    Task PublishAsync(IDomainEvent domainEvent, CancellationToken ct);
    Task PublishManyAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct);
}
