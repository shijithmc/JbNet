using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace JbNet.Tests.Api.Infrastructure;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for API contract tests.
/// Replaces JWT Bearer auth with <see cref="TestAuthHandler"/> and
/// substitutes <see cref="ISender"/> (MediatR) so no real infrastructure is needed.
/// </summary>
public sealed class JbNetApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Pre-configured MediatR substitute. Tests configure expectations on this.</summary>
    public ISender MediatorSubstitute { get; } = Substitute.For<ISender>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // ── Auth: replace JWT Bearer with test handler ──────────────────────
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme    = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

            // ── MediatR: replace ISender with a substitute ───────────────────────
            // This decouples contract tests from real DynamoDB / S3 / SNS / EventBridge.
            services.RemoveAll<ISender>();
            services.AddSingleton(MediatorSubstitute);
        });
    }
}
