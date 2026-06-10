using FluentAssertions;
using JbNet.Domain.Aggregates.Referrals;
using JbNet.Domain.Enums;
using JbNet.Domain.Events;
using JbNet.Domain.Exceptions;
using JbNet.Domain.ValueObjects;

namespace JbNet.Tests.Unit.Domain;

public sealed class ReferralRequestTests
{
    private static readonly UserId SeekerId = UserId.New();
    private static readonly JobId JobId = JobId.New();
    private static readonly UserId IntermediaryId = UserId.New();
    private static readonly UserId FinalReferrerId = UserId.New();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static ReferralRequest CreateRequest(IReadOnlyList<UserId>? hops = null) =>
        ReferralRequest.Create(
            RequestId.New(), SeekerId, JobId,
            "Infosys", "Senior Developer",
            "resumes/user1/resume.pdf",
            "Please refer me",
            hops ?? [IntermediaryId, FinalReferrerId],
            Now);

    [Fact]
    public void Step_01_Create_WithTwoHops_SetsInitialState()
    {
        var request = CreateRequest();

        request.Status.Should().Be(ReferralStatus.Sent);
        request.Hops.Should().HaveCount(2);
        request.Hops[0].Status.Should().Be(HopStatus.Pending);
        request.CurrentHopIndex.Should().Be(0);
        request.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Step_02_Create_WithTwoHops_RaisesCreatedEvent()
    {
        var request = CreateRequest();

        request.DomainEvents.Should().ContainSingle(e => e is ReferralRequestCreatedEvent);
    }

    [Fact]
    public void Step_03_Forward_ByIntermediaryOnFirstHop_AdvancesToSecondHop()
    {
        var request = CreateRequest();

        request.Forward(IntermediaryId, "Passing along", Now.AddHours(1));

        request.Status.Should().Be(ReferralStatus.Forwarded);
        request.Hops[0].Status.Should().Be(HopStatus.Forwarded);
        request.CurrentHopIndex.Should().Be(1);
        request.DomainEvents.Should().Contain(e => e is ReferralRequestForwardedEvent);
    }

    [Fact]
    public void Step_04_Accept_ByFinalReferrer_MovesToAccepted()
    {
        var request = CreateRequest();
        // 2-hop chain: seeker → intermediary → finalReferrer
        // Intermediary forwards → Forwarded (hop 1 is now current)
        request.Forward(IntermediaryId, null, Now.AddHours(1));
        // FinalReferrerId is now at hop 1 (the last hop) — Forward sets ReachedFinalReferrer
        request.Forward(FinalReferrerId, null, Now.AddHours(2));
        // Final referrer now accepts
        request.Accept(FinalReferrerId, Now.AddHours(3));

        request.Status.Should().Be(ReferralStatus.Accepted);
        request.Hops[1].Status.Should().Be(HopStatus.Accepted);
        request.IsActive.Should().BeFalse();
        request.DomainEvents.Should().Contain(e => e is ReferralRequestAcceptedEvent);
    }

    [Fact]
    public void Step_05_Decline_ByIntermediaryOnFirstHop_EndsChain()
    {
        var request = CreateRequest();

        request.Decline(IntermediaryId, Now.AddHours(1));

        request.Status.Should().Be(ReferralStatus.Declined);
        request.Hops[0].Status.Should().Be(HopStatus.Declined);
        request.IsActive.Should().BeFalse();
        request.DomainEvents.Should().Contain(e => e is ReferralRequestDeclinedEvent);
    }

    [Fact]
    public void Step_06_Forward_ByNonCurrentParticipant_ThrowsUnauthorized()
    {
        var request = CreateRequest();
        var notAParticipant = UserId.New();

        var act = () => request.Forward(notAParticipant, null, Now.AddHours(1));

        act.Should().Throw<UnauthorizedHopActionException>();
    }

    [Fact]
    public void Step_07_Forward_ByWrongHopOrder_ThrowsUnauthorized()
    {
        // FinalReferrerId is hop 1 — not eligible on hop 0
        var request = CreateRequest();

        var act = () => request.Forward(FinalReferrerId, null, Now.AddHours(1));

        act.Should().Throw<UnauthorizedHopActionException>();
    }

    [Fact]
    public void Step_08_Withdraw_ByJobSeeker_MovesToWithdrawn()
    {
        var request = CreateRequest();

        request.Withdraw(SeekerId, Now.AddHours(1));

        request.Status.Should().Be(ReferralStatus.Withdrawn);
        request.IsActive.Should().BeFalse();
        request.DomainEvents.Should().Contain(e => e is ReferralRequestWithdrawnEvent);
    }

    [Fact]
    public void Step_09_Withdraw_AfterAccepted_Throws()
    {
        var request = CreateRequest([FinalReferrerId]); // 1-hop: seeker → finalReferrer
        request.Forward(FinalReferrerId, null, Now.AddHours(1));  // → ReachedFinalReferrer
        request.Accept(FinalReferrerId, Now.AddHours(2));         // → Accepted

        var act = () => request.Withdraw(SeekerId, Now.AddHours(3));

        act.Should().Throw<RequestNotActiveException>();
    }

    [Fact]
    public void Step_10_Expire_ActiveRequest_MovesToExpired()
    {
        var request = CreateRequest();

        request.Expire(Now.AddDays(8));

        request.Status.Should().Be(ReferralStatus.Expired);
        request.IsActive.Should().BeFalse();
        request.DomainEvents.Should().Contain(e => e is ReferralRequestExpiredEvent);
    }

    [Fact]
    public void Step_11_Create_SingleHop_ForwardThenAccept()
    {
        // 1-hop chain: seeker → finalReferrer directly
        var request = CreateRequest([FinalReferrerId]);

        request.Status.Should().Be(ReferralStatus.Sent);
        request.Hops.Should().HaveCount(1);

        // FinalReferrerId forwards (last hop) → ReachedFinalReferrer
        request.Forward(FinalReferrerId, null, Now.AddHours(1));
        request.Status.Should().Be(ReferralStatus.ReachedFinalReferrer);

        request.Accept(FinalReferrerId, Now.AddHours(2));
        request.Status.Should().Be(ReferralStatus.Accepted);
    }

    [Fact]
    public void Step_12_IsActiveParticipant_ReturnsTrueForAnyChainParticipant()
    {
        // IsActiveParticipant is used for resume-privacy checks — any hop participant
        // in an active request may view the resume.
        var request = CreateRequest();

        request.IsActiveParticipant(IntermediaryId).Should().BeTrue();
        request.IsActiveParticipant(FinalReferrerId).Should().BeTrue(); // also in chain
        request.IsActiveParticipant(UserId.New()).Should().BeFalse();  // unrelated user
    }

    [Fact]
    public void Step_13_ExpiresAt_IsSevenDaysAfterCreation()
    {
        var request = CreateRequest();

        request.ExpiresAt.Should().BeCloseTo(Now.AddDays(7), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Step_14_ClearDomainEvents_RemovesAllEvents()
    {
        var request = CreateRequest();
        request.DomainEvents.Should().NotBeEmpty();

        request.ClearDomainEvents();

        request.DomainEvents.Should().BeEmpty();
    }
}
