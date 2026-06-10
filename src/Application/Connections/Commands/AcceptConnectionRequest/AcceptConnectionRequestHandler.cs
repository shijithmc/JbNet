using JbNet.Application.Common;
using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.Events;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JbNet.Application.Connections.Commands.AcceptConnectionRequest;

public sealed class AcceptConnectionRequestHandler(
    IUserRepository userRepository,
    IConnectionRepository connectionRepository,
    IEventPublisher eventPublisher,
    ILogger<AcceptConnectionRequestHandler> logger) : IRequestHandler<AcceptConnectionRequestCommand, AcceptConnectionRequestResult>
{
    public async Task<AcceptConnectionRequestResult> Handle(AcceptConnectionRequestCommand command, CancellationToken ct)
    {
        var accepterId = UserId.From(command.AccepterId);
        var requesterId = UserId.From(command.RequesterId);
        var now = DateTimeOffset.UtcNow;

        // Load the pending request (owned by requester → accepter)
        var pendingConnection = await connectionRepository.GetAsync(requesterId, accepterId, ct)
            ?? throw new InvalidOperationException("Connection request not found.");

        pendingConnection.Accept(now);

        // Create the reverse direction record
        var reverseId = ConnectionId.New();
        var reverseConnection = Connection.CreatePending(reverseId, accepterId, requesterId, null, now);
        reverseConnection.Accept(now);

        // Increment connection counts on both users
        var requester = await userRepository.GetByIdAsync(requesterId, ct);
        var accepter = await userRepository.GetByIdAsync(accepterId, ct);
        requester?.IncrementConnectionCount();
        accepter?.IncrementConnectionCount();

        await connectionRepository.SaveBothDirectionsAsync(pendingConnection, reverseConnection, ct);
        if (requester != null) await userRepository.SaveAsync(requester, ct);
        if (accepter != null) await userRepository.SaveAsync(accepter, ct);

        await eventPublisher.PublishAsync(
            new ConnectionAcceptedEvent(Guid.NewGuid().ToString(), now, requesterId, accepterId), ct);

        logger.LogInformation(
            "Connection accepted: {RequesterId} ↔ {AccepterId}",
            command.RequesterId, command.AccepterId);

        return new AcceptConnectionRequestResult("Accepted");
    }
}
