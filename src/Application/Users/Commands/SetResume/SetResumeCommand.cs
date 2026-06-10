using MediatR;

namespace JbNet.Application.Users.Commands.SetResume;

/// <summary>Records that a user has uploaded their resume to S3. Client uploads directly via presigned URL; API confirms.</summary>
public sealed record SetResumeCommand(
    string UserId,
    string FileName,
    long SizeBytes
) : IRequest<SetResumeResult>;

public sealed record SetResumeResult(string UploadUrl, string S3Key);
