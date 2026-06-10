using JbNet.Application.Common;
using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.Events;
using JbNet.Domain.Exceptions;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JbNet.Application.Connections.Commands.SendConnectionRequest;

public sealed class SendConnectionRequestHandler(
    IUserRepository userRepository,
    IConnectionRepository connectionRepository,
    IEventPublisher eventPublisher,
    ILogger<SendConnectionRequestHandler> logger) : IRequestHandler<SendConnectionRequestCommand, SendConnectionRequestResult>
{
    public async Task<SendConnectionRequestResult> Handle(SendConnectionRequestCommand command, CancellationToken ct)
    {
        var requesterId = UserId.From(command.RequesterId);
        var targetId = UserId.From(command.TargetUserId);
        var now = DateTimeOffset.UtcNow;

        if (requesterId == targetId)
            throw new ArgumentException("Cannot connect with yourself.");

        var requester = await userRepository.GetByIdAsync(requesterId, ct)
            ?? throw new InvalidOperationException("Requester not found.");

        var target = await userRepository.GetByIdAsync(targetId, ct)
            ?? throw new InvalidOperationException("Target user not found.");

        var existing = await connectionRepository.GetAsync(requesterId, targetId, ct);
        if (existing != null)
            throw new ConnectionAlreadyExistsException(command.TargetUserId);

        var connectionId = ConnectionId.New();
        var note = command.Note;
        if (note?.Length > 150) note = note[..150];

        var connection = Connection.CreatePending(connectionId, requesterId, targetId, note, now);

        await connectionRepository.SaveAsync(connection, ct);

        logger.LogInformation(
            "Connection request {ConnId} sent from {RequesterId} to {TargetId}",
            connectionId.Value, command.RequesterId, command.TargetUserId);

        return new SendConnectionRequestResult(connectionId.Value);
    }
}
