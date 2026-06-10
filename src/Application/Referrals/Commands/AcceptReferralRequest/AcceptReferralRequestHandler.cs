using JbNet.Application.Common;
using JbNet.Domain.Aggregates.Referrals;
using JbNet.Domain.Events;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JbNet.Application.Referrals.Commands.AcceptReferralRequest;

public sealed class AcceptReferralRequestHandler(
    IReferralRequestRepository referralRepository,
    IUserRepository userRepository,
    IEventPublisher eventPublisher,
    ILogger<AcceptReferralRequestHandler> logger) : IRequestHandler<AcceptReferralRequestCommand, AcceptReferralRequestResult>
{
    public async Task<AcceptReferralRequestResult> Handle(AcceptReferralRequestCommand command, CancellationToken ct)
    {
        var requestId = RequestId.From(command.RequestId);
        var actingUserId = UserId.From(command.ActingUserId);
        var now = DateTimeOffset.UtcNow;

        var request = await referralRepository.GetByIdAsync(requestId, ct)
            ?? throw new InvalidOperationException($"Request '{command.RequestId}' not found.");

        var referrer = await userRepository.GetByIdAsync(actingUserId, ct)
            ?? throw new InvalidOperationException($"User '{command.ActingUserId}' not found.");

        request.Accept(actingUserId, now);

        // Decrement the job seeker's active request count — slot is freed
        var jobSeeker = await userRepository.GetByIdAsync(request.JobSeekerId, ct);
        jobSeeker?.DecrementActiveReferralCount();

        // Patch referrer name into the event (domain event had empty name — resolved here)
        var events = request.DomainEvents
            .OfType<ReferralRequestAcceptedEvent>()
            .Select(e => e with { ReferrerName = referrer.FullName })
            .Cast<Domain.Events.IDomainEvent>()
            .ToList();

        await referralRepository.SaveAsync(request, ct);
        if (jobSeeker != null) await userRepository.SaveAsync(jobSeeker, ct);
        await eventPublisher.PublishManyAsync(events, ct);
        request.ClearDomainEvents();

        logger.LogInformation("ReferralRequest {RequestId} accepted by {UserId}", command.RequestId, command.ActingUserId);

        return new AcceptReferralRequestResult(command.RequestId, referrer.FullName, request.CompanyName);
    }
}
