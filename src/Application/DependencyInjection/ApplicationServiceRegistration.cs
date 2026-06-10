using FluentValidation;
using JbNet.Domain.Repositories;
using JbNet.Domain.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace JbNet.Application.DependencyInjection;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationServiceRegistration).Assembly));
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceRegistration).Assembly);

        // Domain services
        services.AddScoped<ReferralPathDiscoveryService>();

        return services;
    }
}
