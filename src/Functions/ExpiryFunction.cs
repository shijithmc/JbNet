using Amazon.Lambda.CloudWatchEvents;
using Amazon.Lambda.Core;
using JbNet.Application.Common;
using JbNet.Domain.Repositories;
using JbNet.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace JbNet.Functions;

/// <summary>
/// Triggered hourly by EventBridge Scheduler. Finds referral requests past their 7-day expiry window
/// and marks them Expired, publishing ExpiryEvents to trigger chain notifications.
/// </summary>
public sealed class ExpiryFunction
{
    private readonly IServiceProvider _services;

    public ExpiryFunction()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddInfrastructureServices(configuration);

        _services = services.BuildServiceProvider();
    }

    public async Task Handler(CloudWatchEvent<object> @event, ILambdaContext context)
    {
        var logger = _services.GetRequiredService<ILogger<ExpiryFunction>>();
        logger.LogInformation("ExpiryFunction triggered at {Time}", context.InvokedFunctionArn);

        await using var scope = _services.CreateAsyncScope();
        var referralRepo = scope.ServiceProvider.GetRequiredService<IReferralRequestRepository>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var candidates = await referralRepo.GetExpiredCandidatesAsync(
            olderThanDays: 7,
            CancellationToken.None);

        int expired = 0;
        foreach (var request in candidates)
        {
            try
            {
                request.Expire(DateTimeOffset.UtcNow);
                await referralRepo.SaveAsync(request, CancellationToken.None);
                await eventPublisher.PublishManyAsync(request.DomainEvents, CancellationToken.None);
                request.ClearDomainEvents();
                expired++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to expire request {RequestId}", request.Id.Value);
            }
        }

        logger.LogInformation("ExpiryFunction: expired {Count}/{Total} requests", expired, candidates.Count);
    }
}
