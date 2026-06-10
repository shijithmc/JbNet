using Amazon.S3;
using Amazon.S3.Model;
using JbNet.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JbNet.Infrastructure.S3;

public sealed class S3ResumeStorageService(
    IAmazonS3 s3Client,
    IOptions<S3Options> options,
    ILogger<S3ResumeStorageService> logger) : IResumeStorageService
{
    private readonly string _bucketName = options.Value.ResumeBucketName;

    public async Task<string> GenerateUploadUrlAsync(string s3Key, CancellationToken ct)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            Verb = HttpVerb.PUT,
            ContentType = "application/pdf",
            Expires = DateTime.UtcNow.AddMinutes(5)
        };

        return await s3Client.GetPreSignedURLAsync(request);
    }

    public async Task<string> GenerateDownloadUrlAsync(string s3Key, CancellationToken ct)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(15)
        };

        return await s3Client.GetPreSignedURLAsync(request);
    }

    public async Task DeleteAsync(string s3Key, CancellationToken ct)
    {
        try
        {
            await s3Client.DeleteObjectAsync(_bucketName, s3Key, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Object already gone — idempotent delete is fine
            logger.LogWarning("Attempted to delete non-existent S3 object {Key}", s3Key);
        }
    }

    public string BuildS3Key(string userId) => $"resumes/{userId}/resume.pdf";
}

public sealed class S3Options
{
    public const string SectionName = "S3";
    public string ResumeBucketName { get; set; } = string.Empty;
}
