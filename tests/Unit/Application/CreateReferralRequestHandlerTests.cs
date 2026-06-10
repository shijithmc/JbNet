using FluentAssertions;
using JbNet.Application.Common;
using JbNet.Application.Referrals.Commands.CreateReferralRequest;
using JbNet.Domain.Aggregates.Jobs;
using JbNet.Domain.Aggregates.Referrals;
using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.Exceptions;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace JbNet.Tests.Unit.Application;

public sealed class CreateReferralRequestHandlerTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IJobPostingRepository _jobRepo = Substitute.For<IJobPostingRepository>();
    private readonly IReferralRequestRepository _referralRepo = Substitute.For<IReferralRequestRepository>();
    private readonly IResumeStorageService _resumeStorage = Substitute.For<IResumeStorageService>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();

    private readonly CreateReferralRequestHandler _handler;

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    public CreateReferralRequestHandlerTests()
    {
        _handler = new CreateReferralRequestHandler(
            _userRepo, _jobRepo, _referralRepo,
            _resumeStorage, _eventPublisher,
            NullLogger<CreateReferralRequestHandler>.Instance);
    }

    private static User BuildUserWithResume()
    {
        var user = User.Create(UserId.New(), "Test User", "test@test.com", Now);
        user.SetResume("resumes/uid/resume.pdf", "resume.pdf", 100_000, Now);
        return user;
    }

    private static JobPosting BuildActiveJob() =>
        JobPosting.Create(JobId.New(), "Infosys", "Senior Engineer",
            "Full time role at Infosys.", "Bengaluru", null, Now);

    [Fact]
    public async Task Step_01_Handle_ValidCommand_CreatesReferralAndReturns201()
    {
        var jobSeeker = BuildUserWithResume();
        var job = BuildActiveJob();
        var hop1 = UserId.New();
        var hop2 = UserId.New();

        _userRepo.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(jobSeeker);
        _jobRepo.GetByIdAsync(Arg.Any<JobId>(), Arg.Any<CancellationToken>()).Returns(job);
        _referralRepo.GetActiveByJobSeekerAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ReferralRequest>());
        _referralRepo.GetCooldownAsync(Arg.Any<UserId>(), Arg.Any<JobId>(), Arg.Any<CancellationToken>())
            .Returns((ReferralCooldown?)null);

        var command = new CreateReferralRequestCommand(
            jobSeeker.Id.Value, job.Id.Value,
            [hop1.Value, hop2.Value], "Please refer me!");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.RequestId.Should().NotBeNullOrEmpty();
        result.Status.Should().Be("Sent");
        await _referralRepo.Received(1).SaveAsync(Arg.Any<ReferralRequest>(), Arg.Any<CancellationToken>());
        await _userRepo.Received(1).SaveAsync(jobSeeker, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Step_02_Handle_UserWithNoResume_ThrowsInvalidOperation()
    {
        var jobSeeker = User.Create(UserId.New(), "No Resume", "noresume@test.com", Now);
        // No resume set

        _userRepo.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(jobSeeker);

        var command = new CreateReferralRequestCommand(
            jobSeeker.Id.Value, JobId.New().Value, ["hop1", "hop2"], null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*resume*");
    }

    [Fact]
    public async Task Step_03_Handle_UserAtRequestLimit_ThrowsActiveRequestLimitExceeded()
    {
        var jobSeeker = BuildUserWithResume();
        var job = BuildActiveJob();

        // Simulate 5 active requests already
        var fakeActiveRequests = Enumerable.Range(0, User.MaxActiveRequests)
            .Select(_ => ReferralRequest.Create(
                RequestId.New(), jobSeeker.Id, JobId.New(),
                "Company", "Title", "key", null, [UserId.New()], Now))
            .ToList()
            .AsReadOnly();

        _userRepo.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(jobSeeker);
        _jobRepo.GetByIdAsync(Arg.Any<JobId>(), Arg.Any<CancellationToken>()).Returns(job);
        _referralRepo.GetActiveByJobSeekerAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(fakeActiveRequests);

        var command = new CreateReferralRequestCommand(
            jobSeeker.Id.Value, job.Id.Value, [UserId.New().Value], null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ActiveRequestLimitExceededException>();
    }

    [Fact]
    public async Task Step_04_Handle_DuplicateActiveRequestForSameJob_ThrowsDuplicateRequest()
    {
        var jobSeeker = BuildUserWithResume();
        var job = BuildActiveJob();

        var existingRequest = ReferralRequest.Create(
            RequestId.New(), jobSeeker.Id, job.Id,
            "Infosys", "Senior Engineer", "key", null, [UserId.New()], Now);

        _userRepo.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(jobSeeker);
        _jobRepo.GetByIdAsync(Arg.Any<JobId>(), Arg.Any<CancellationToken>()).Returns(job);
        _referralRepo.GetActiveByJobSeekerAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(new List<ReferralRequest> { existingRequest }.AsReadOnly());

        var command = new CreateReferralRequestCommand(
            jobSeeker.Id.Value, job.Id.Value, [UserId.New().Value], null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateRequestException>();
    }

    [Fact]
    public async Task Step_05_Handle_ActiveCooldown_ThrowsCooldownActive()
    {
        var jobSeeker = BuildUserWithResume();
        var job = BuildActiveJob();

        var cooldown = ReferralCooldown.Create(jobSeeker.Id, job.Id, Now.AddDays(-5)); // 5d old, 25d remaining

        _userRepo.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(jobSeeker);
        _jobRepo.GetByIdAsync(Arg.Any<JobId>(), Arg.Any<CancellationToken>()).Returns(job);
        _referralRepo.GetActiveByJobSeekerAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ReferralRequest>());
        _referralRepo.GetCooldownAsync(Arg.Any<UserId>(), Arg.Any<JobId>(), Arg.Any<CancellationToken>())
            .Returns(cooldown);

        var command = new CreateReferralRequestCommand(
            jobSeeker.Id.Value, job.Id.Value, [UserId.New().Value], null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<CooldownActiveException>();
    }

    [Fact]
    public async Task Step_06_Handle_ExpiredJob_ThrowsInvalidOperation()
    {
        var jobSeeker = BuildUserWithResume();
        // Create a job and simulate expiry by using a creation date 91+ days ago
        var oldJob = JobPosting.Create(JobId.New(), "Wipro", "Dev",
            "Dev role at Wipro.", "Delhi", null, Now.AddDays(-91));

        _userRepo.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(jobSeeker);
        _jobRepo.GetByIdAsync(Arg.Any<JobId>(), Arg.Any<CancellationToken>()).Returns(oldJob);
        _referralRepo.GetActiveByJobSeekerAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ReferralRequest>());

        var command = new CreateReferralRequestCommand(
            jobSeeker.Id.Value, oldJob.Id.Value, [UserId.New().Value], null);

        var act = () => _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }
}
