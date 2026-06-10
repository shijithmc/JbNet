using JbNet.Domain.Repositories;
using JbNet.Domain.Services;
using JbNet.Domain.ValueObjects;
using MediatR;

namespace JbNet.Application.Referrals.Queries.DiscoverReferralPaths;

public sealed class DiscoverReferralPathsHandler(
    IJobPostingRepository jobRepository,
    ReferralPathDiscoveryService pathDiscovery) : IRequestHandler<DiscoverReferralPathsQuery, DiscoverReferralPathsResult>
{
    public async Task<DiscoverReferralPathsResult> Handle(DiscoverReferralPathsQuery query, CancellationToken ct)
    {
        var jobSeekerId = UserId.From(query.JobSeekerId);
        var jobId = JobId.From(query.JobId);

        var job = await jobRepository.GetByIdAsync(jobId, ct)
            ?? throw new InvalidOperationException($"Job '{query.JobId}' not found.");

        var paths = await pathDiscovery.DiscoverPathsAsync(
            jobSeekerId,
            job.NormalisedCompanyName,
            maxHops: 2,
            ct);

        var pathDtos = paths.Select(p => new ReferralPathDto(
            p.TotalHops,
            p.Hops.Select(h => new ReferralPathHopDto(
                h.UserId.Value,
                h.FullName,
                h.Headline,
                h.EmployerName,
                h.IsAtTargetCompany)).ToList()
        )).ToList();

        return new DiscoverReferralPathsResult(
            job.Id.Value,
            job.CompanyName,
            job.Title,
            pathDtos);
    }
}
