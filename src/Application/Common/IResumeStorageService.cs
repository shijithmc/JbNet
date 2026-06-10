namespace JbNet.Application.Common;

/// <summary>Abstraction for resume PDF storage. Implemented by S3ResumeStorageService in Infrastructure.</summary>
public interface IResumeStorageService
{
    /// <summary>Returns a presigned PUT URL the client uses to upload the resume directly to S3. TTL = 5 minutes.</summary>
    Task<string> GenerateUploadUrlAsync(string s3Key, CancellationToken ct);

    /// <summary>Returns a presigned GET URL for reading the resume. TTL = 15 minutes. Access is validated by caller before invoking.</summary>
    Task<string> GenerateDownloadUrlAsync(string s3Key, CancellationToken ct);

    /// <summary>Deletes the resume object from S3. Called on replacement or withdrawal.</summary>
    Task DeleteAsync(string s3Key, CancellationToken ct);

    /// <summary>Constructs the deterministic S3 key for a user's resume.</summary>
    string BuildS3Key(string userId);
}
