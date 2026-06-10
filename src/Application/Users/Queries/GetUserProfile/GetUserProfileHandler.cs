using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;

namespace JbNet.Application.Users.Queries.GetUserProfile;

public sealed class GetUserProfileHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserProfileQuery, GetUserProfileResult?>
{
    public async Task<GetUserProfileResult?> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(UserId.From(request.UserId), cancellationToken);
        if (user is null) return null;

        return new GetUserProfileResult(
            user.Id.Value,
            user.FullName,
            user.Email,
            user.Headline,
            user.EmployerName,
            user.City,
            user.ProfilePhotoUrl,
            user.ResumeS3Key is not null,
            user.ConnectionCount,
            user.ActiveReferralCount,
            user.CreatedAt);
    }
}
