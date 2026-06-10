using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JbNet.Tests.Api.Infrastructure;

/// <summary>
/// Fake authentication handler for API contract tests.
/// Authenticates when an <c>Authorization</c> header is present (any value);
/// returns <see cref="AuthenticateResult.NoResult"/> when absent — causing the
/// framework to challenge with 401 Unauthorized.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName    = "TestAuth";
    public const string DefaultUserId = "test-user-sub-001";

    /// <summary>
    /// Fake bearer token value to attach on authenticated requests.
    /// Matches no real JWT; the handler validates only its presence.
    /// </summary>
    public const string FakeBearer = "Bearer test-token-valid";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No Authorization header → NoResult → framework returns 401
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader)
            || string.IsNullOrWhiteSpace(authHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim("sub",                 DefaultUserId),
            new Claim(ClaimTypes.Name,       "Test User"),
            new Claim(ClaimTypes.Email,      "testuser@example.com"),
        };

        var identity  = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
