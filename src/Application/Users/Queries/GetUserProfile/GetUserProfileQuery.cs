using MediatR;

namespace JbNet.Application.Users.Queries.GetUserProfile;

public sealed record GetUserProfileQuery(string UserId) : IRequest<GetUserProfileResult?>;

public sealed record GetUserProfileResult(
    string UserId,
    string FullName,
    string Email,
    string Headline,
    string? EmployerName,
    string? City,
    string? ProfilePhotoUrl,
    bool HasResume,
    int ConnectionCount,
    int ActiveReferralCount,
    DateTimeOffset CreatedAt
);
