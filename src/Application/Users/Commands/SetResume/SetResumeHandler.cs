using JbNet.Application.Common;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JbNet.Application.Users.Commands.SetResume;

public sealed class SetResumeHandler(
    IUserRepository userRepository,
    IResumeStorageService resumeStorage,
    ILogger<SetResumeHandler> logger) : IRequestHandler<SetResumeCommand, SetResumeResult>
{
    private const long MaxResumeSizeBytes = 5 * 1024 * 1024; // 5 MB

    public async Task<SetResumeResult> Handle(SetResumeCommand command, CancellationToken ct)
    {
        if (command.SizeBytes > MaxResumeSizeBytes)
            throw new InvalidOperationException($"Resume must be under 5 MB. Uploaded: {command.SizeBytes / 1024 / 1024} MB.");

        var userId = UserId.From(command.UserId);
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"User '{command.UserId}' not found.");

        var oldKey = user.ResumeS3Key;
        var newKey = resumeStorage.BuildS3Key(command.UserId);

        // Generate presigned upload URL (client uploads directly to S3)
        var uploadUrl = await resumeStorage.GenerateUploadUrlAsync(newKey, ct);

        user.SetResume(newKey, command.FileName, command.SizeBytes, DateTimeOffset.UtcNow);
        await userRepository.SaveAsync(user, ct);

        // Delete old resume after profile updated (prevents orphan)
        if (!string.IsNullOrEmpty(oldKey) && oldKey != newKey)
            await resumeStorage.DeleteAsync(oldKey, ct);

        logger.LogInformation("Resume updated for user {UserId}, key {S3Key}", command.UserId, newKey);

        return new SetResumeResult(uploadUrl, newKey);
    }
}
