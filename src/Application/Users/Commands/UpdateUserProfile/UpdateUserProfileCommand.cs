using MediatR;

namespace JbNet.Application.Users.Commands.UpdateUserProfile;

public sealed record UpdateUserProfileCommand(
    string UserId,
    string FullName,
    string Headline,
    string? EmployerName,
    string? City
) : IRequest<UpdateUserProfileResult>;

public sealed record UpdateUserProfileResult(string UserId, string? EmployerName);
