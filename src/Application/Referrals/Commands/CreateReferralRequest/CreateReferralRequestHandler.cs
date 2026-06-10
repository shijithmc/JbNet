using JbNet.Application.Common;
using JbNet.Domain.Aggregates.Referrals;
using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.Exceptions;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JbNet.Application.Referrals.Commands.CreateReferralRequest;

public sealed class CreateReferralRequestHandler(
    IUserRepository userRepository,
    IJobPostingRepository jobRepository,
    IReferralRequestRepository referralRepository,
    IResumeStorageService resumeStorage,
    IEventPublisher eventPublisher,
    ILogger<CreateReferralRequestHandler> logger) : IRequestHandler<CreateReferralRequestCommand, CreateReferralRequestResult>
{
    public async Task<CreateReferralRequestResult> Handle(
        CreateReferralRequestCommand command,
        CancellationToken ct)
    {
        var jobSeekerId = UserId.From(command.JobSeekerId);
        var jobId = JobId.From(command.JobId);
        var now = DateTimeOffset.UtcNow;

        // 1. Verify job seeker exists and has a resume
        var jobSeeker = await userRepository.GetByIdAsync(jobSeekerId, ct)
            ?? throw new InvalidOperationException($"User '{command.JobSeekerId}' not found.");

        if (string.IsNullOrEmpty(jobSeeker.ResumeS3Key))
            throw new InvalidOperationException("Upload a resume before requesting a referral.");

        // 2. Verify job exists
        var job = await jobRepository.GetByIdAsync(jobId, ct)
            ?? throw new InvalidOperationException($"Job '{command.JobId}' not found.");

        if (job.IsExpired(now))
            throw new InvalidOperationException("This job posting has expired.");

        // 3. Check active request limit
        var activeRequests = await referralRepository.GetActiveByJobSeekerAsync(jobSeekerId, ct);
        if (activeRequests.Count >= User.MaxActiveRequests)
            throw new ActiveRequestLimitExceededException(User.MaxActiveRequests);

        // 4. Check duplicate active request for same job
        if (activeRequests.Any(r => r.JobId == jobId))
            throw new DuplicateRequestException(command.JobId);

        // 5. Check cooldown
        var cooldown = await referralRepository.GetCooldownAsync(jobSeekerId, jobId, ct);
        if (cooldown != null && !cooldown.IsExpired(now))
            throw new CooldownActiveException(cooldown.ExpiresAt);

        // 6. Build referral request
        var hopIds = command.HopParticipantIds.Select(UserId.From).ToList();
        var requestId = RequestId.New();

        var request = ReferralRequest.Create(
            requestId, jobSeekerId, jobId,
            job.CompanyName, job.Title,
            jobSeeker.ResumeS3Key,
            command.PersonalNote,
            hopIds, now);

        // 7. Increment seeker's active count (enforces invariant)
        jobSeeker.IncrementActiveReferralCount();

        // 8. Persist
        await referralRepository.SaveAsync(request, ct);
        await userRepository.SaveAsync(jobSeeker, ct);

        // 9. Publish domain events (triggers notification Lambda)
        await eventPublisher.PublishManyAsync(request.DomainEvents, ct);
        request.ClearDomainEvents();

        logger.LogInformation(
            "ReferralRequest {RequestId} created by {UserId} for Job {JobId}",
            requestId.Value, command.JobSeekerId, command.JobId);

        return new CreateReferralRequestResult(requestId.Value, request.Status.ToString());
    }
}
