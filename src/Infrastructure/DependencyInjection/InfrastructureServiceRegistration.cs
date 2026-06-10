using Amazon.DynamoDBv2;
using Amazon.EventBridge;
using Amazon.S3;
using JbNet.Application.Common;
using JbNet.Domain.Repositories;
using JbNet.Infrastructure.DynamoDB;
using JbNet.Infrastructure.DynamoDB.Repositories;
using JbNet.Infrastructure.EventBridge;
using JbNet.Infrastructure.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JbNet.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options
        services.Configure<DynamoDbOptions>(configuration.GetSection(DynamoDbOptions.SectionName));
        services.Configure<S3Options>(configuration.GetSection(S3Options.SectionName));
        services.Configure<EventBridgeOptions>(configuration.GetSection(EventBridgeOptions.SectionName));

        // AWS SDK clients — Lambda environment provides credentials via execution role
        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var opts = configuration.GetSection(DynamoDbOptions.SectionName).Get<DynamoDbOptions>();
            var config = new AmazonDynamoDBConfig();
            if (!string.IsNullOrEmpty(opts?.ServiceUrl))
                config.ServiceURL = opts.ServiceUrl; // DynamoDB Local for dev
            return new AmazonDynamoDBClient(config);
        });

        services.AddSingleton<IAmazonS3, AmazonS3Client>();
        services.AddSingleton<IAmazonEventBridge, AmazonEventBridgeClient>();

        // Repositories
        services.AddScoped<IUserRepository, DynamoUserRepository>();
        services.AddScoped<IConnectionRepository, DynamoConnectionRepository>();
        services.AddScoped<IJobPostingRepository, DynamoJobPostingRepository>();
        services.AddScoped<IReferralRequestRepository, DynamoReferralRequestRepository>();

        // Services
        services.AddScoped<IResumeStorageService, S3ResumeStorageService>();
        services.AddScoped<IEventPublisher, EventBridgePublisher>();

        return services;
    }
}
