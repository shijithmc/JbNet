using JbNet.Application.Common;
using JbNet.Domain.Aggregates.Referrals;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JbNet.Application.Referrals.Commands.DeclineReferralRequest;

public sealed class DeclineReferralRequestHandler(
    IReferralRequestRepository referralRepository,
    IUserRepository userRepository,
    IEventPublisher eventPublisher,
    ILogger<DeclineReferralRequestHandler> logger) : IRequestHandler<DeclineReferralRequestCommand, DeclineReferralRequestResult>
{
    public async Task<DeclineReferralRequestResult> Handle(DeclineReferralRequestCommand command, CancellationToken ct)
    {
        var requestId = RequestId.From(command.RequestId);
        var actingUserId = UserId.From(command.ActingUserId);
        var now = DateTimeOffset.UtcNow;

        var request = await referralRepository.GetByIdAsync(requestId, ct)
            ?? throw new InvalidOperationException($"Request '{command.RequestId}' not found.");

        request.Decline(actingUserId, now);

        // Free the slot on the job seeker's active count
        var jobSeeker = await userRepository.GetByIdAsync(request.JobSeekerId, ct);
        jobSeeker?.DecrementActiveReferralCount();

        // Record 30-day cooldown
        var cooldown = ReferralCooldown.Create(request.JobSeekerId, request.JobId, now);

        await referralRepository.SaveAsync(request, ct);
        await referralRepository.SaveCooldownAsync(cooldown, ct);
        if (jobSeeker != null) await userRepository.SaveAsync(jobSeeker, ct);
        await eventPublisher.PublishManyAsync(request.DomainEvents, ct);
        request.ClearDomainEvents();

        logger.LogInformation("ReferralRequest {RequestId} declined by participant {UserId}", command.RequestId, command.ActingUserId);

        return new DeclineReferralRequestResult(command.RequestId, request.Status.ToString());
    }
}
