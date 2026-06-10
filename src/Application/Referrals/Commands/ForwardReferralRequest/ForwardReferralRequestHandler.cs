using JbNet.Application.Common;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JbNet.Application.Referrals.Commands.ForwardReferralRequest;

public sealed class ForwardReferralRequestHandler(
    IReferralRequestRepository referralRepository,
    IEventPublisher eventPublisher,
    ILogger<ForwardReferralRequestHandler> logger) : IRequestHandler<ForwardReferralRequestCommand, ForwardReferralRequestResult>
{
    public async Task<ForwardReferralRequestResult> Handle(ForwardReferralRequestCommand command, CancellationToken ct)
    {
        var requestId = RequestId.From(command.RequestId);
        var actingUserId = UserId.From(command.ActingUserId);
        var now = DateTimeOffset.UtcNow;

        var request = await referralRepository.GetByIdAsync(requestId, ct)
            ?? throw new InvalidOperationException($"Request '{command.RequestId}' not found.");

        request.Forward(actingUserId, command.ForwardNote, now);

        await referralRepository.SaveAsync(request, ct);
        await eventPublisher.PublishManyAsync(request.DomainEvents, ct);
        request.ClearDomainEvents();

        logger.LogInformation(
            "ReferralRequest {RequestId} forwarded by {UserId} → status {Status}",
            command.RequestId, command.ActingUserId, request.Status);

        return new ForwardReferralRequestResult(command.RequestId, request.Status.ToString());
    }
}
