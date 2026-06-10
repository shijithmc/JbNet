using JbNet.Application.Common;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JbNet.Application.Referrals.Commands.WithdrawReferralRequest;

public sealed class WithdrawReferralRequestHandler(
    IReferralRequestRepository referralRepository,
    IUserRepository userRepository,
    IEventPublisher eventPublisher,
    ILogger<WithdrawReferralRequestHandler> logger) : IRequestHandler<WithdrawReferralRequestCommand, WithdrawReferralRequestResult>
{
    public async Task<WithdrawReferralRequestResult> Handle(WithdrawReferralRequestCommand command, CancellationToken ct)
    {
        var requestId = RequestId.From(command.RequestId);
        var jobSeekerId = UserId.From(command.JobSeekerId);
        var now = DateTimeOffset.UtcNow;

        var request = await referralRepository.GetByIdAsync(requestId, ct)
            ?? throw new InvalidOperationException($"Request '{command.RequestId}' not found.");

        request.Withdraw(jobSeekerId, now);

        var jobSeeker = await userRepository.GetByIdAsync(jobSeekerId, ct);
        jobSeeker?.DecrementActiveReferralCount();

        await referralRepository.SaveAsync(request, ct);
        if (jobSeeker != null) await userRepository.SaveAsync(jobSeeker, ct);
        await eventPublisher.PublishManyAsync(request.DomainEvents, ct);
        request.ClearDomainEvents();

        logger.LogInformation("ReferralRequest {RequestId} withdrawn by {UserId}", command.RequestId, command.JobSeekerId);

        return new WithdrawReferralRequestResult(command.RequestId);
    }
}
