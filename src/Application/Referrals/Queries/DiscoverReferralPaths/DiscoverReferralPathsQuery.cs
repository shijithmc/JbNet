using JbNet.Domain.Services;
using MediatR;

namespace JbNet.Application.Referrals.Queries.DiscoverReferralPaths;

/// <summary>Discovers referral paths from the job seeker to employees at the target company. BFS up to 2 hops.</summary>
public sealed record DiscoverReferralPathsQuery(
    string JobSeekerId,
    string JobId
) : IRequest<DiscoverReferralPathsResult>;

public sealed record DiscoverReferralPathsResult(
    string JobId,
    string CompanyName,
    string JobTitle,
    IReadOnlyList<ReferralPathDto> Paths
);

public sealed record ReferralPathDto(
    int TotalHops,
    IReadOnlyList<ReferralPathHopDto> Hops
);

public sealed record ReferralPathHopDto(
    string UserId,
    string FullName,
    string Headline,
    string EmployerName,
    bool IsAtTargetCompany
);
