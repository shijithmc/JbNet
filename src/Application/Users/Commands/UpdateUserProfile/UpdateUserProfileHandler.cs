using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JbNet.Application.Users.Commands.UpdateUserProfile;

public sealed class UpdateUserProfileHandler(
    IUserRepository userRepository,
    ILogger<UpdateUserProfileHandler> logger) : IRequestHandler<UpdateUserProfileCommand, UpdateUserProfileResult>
{
    public async Task<UpdateUserProfileResult> Handle(UpdateUserProfileCommand command, CancellationToken ct)
    {
        var userId = UserId.From(command.UserId);
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"User '{command.UserId}' not found.");

        user.UpdateProfile(
            command.FullName,
            command.Headline,
            command.EmployerName,
            command.City,
            DateTimeOffset.UtcNow);

        await userRepository.SaveAsync(user, ct);

        logger.LogInformation("Profile updated for user {UserId}", command.UserId);

        return new UpdateUserProfileResult(command.UserId, user.EmployerName);
    }
}
