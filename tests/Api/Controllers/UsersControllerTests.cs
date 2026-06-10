using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JbNet.Application.Users.Commands.SetResume;
using JbNet.Application.Users.Commands.UpdateUserProfile;
using JbNet.Application.Users.Queries.GetUserProfile;
using JbNet.Tests.Api.Infrastructure;
using MediatR;
using NSubstitute;

namespace JbNet.Tests.Api.Controllers;

/// <summary>
/// API contract tests for <c>/users</c> endpoints.
/// MediatR <see cref="ISender"/> is substituted — no real infrastructure required.
/// </summary>
public sealed class UsersControllerTests : IClassFixture<JbNetApiFactory>, IDisposable
{
    private readonly JbNetApiFactory _factory;
    private readonly HttpClient _authenticatedClient;
    private readonly HttpClient _anonymousClient;

    private static readonly DateTimeOffset FixedNow =
        new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    public UsersControllerTests(JbNetApiFactory factory)
    {
        _factory             = factory;
        _authenticatedClient = factory.CreateClient();
        _authenticatedClient.DefaultRequestHeaders.Add("Authorization", TestAuthHandler.FakeBearer);

        _anonymousClient = factory.CreateClient();
        // No Authorization header — TestAuthHandler returns NoResult → 401
    }

    public void Dispose()
    {
        _authenticatedClient.Dispose();
        _anonymousClient.Dispose();
    }

    // ── GET /users/me ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        var response = await _anonymousClient.GetAsync("/users/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_UserNotFound_Returns404()
    {
        _factory.MediatorSubstitute
            .Send(Arg.Any<GetUserProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns((GetUserProfileResult?)null);

        var response = await _authenticatedClient.GetAsync("/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMe_UserExists_Returns200WithProfile()
    {
        var expected = new GetUserProfileResult(
            UserId:             TestAuthHandler.DefaultUserId,
            FullName:           "Alice Smith",
            Email:              "alice@example.com",
            Headline:           "Software Engineer",
            EmployerName:       "Acme Ltd",
            City:               "Sydney",
            ProfilePhotoUrl:    null,
            HasResume:          false,
            ConnectionCount:    3,
            ActiveReferralCount: 1,
            CreatedAt:          FixedNow);

        _factory.MediatorSubstitute
            .Send(Arg.Any<GetUserProfileQuery>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var response = await _authenticatedClient.GetAsync("/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<GetUserProfileResult>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(TestAuthHandler.DefaultUserId);
        body.FullName.Should().Be("Alice Smith");
        body.ConnectionCount.Should().Be(3);
    }

    // ── PUT /users/me ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateMe_Unauthenticated_Returns401()
    {
        var response = await _anonymousClient.PutAsJsonAsync("/users/me",
            new { FullName = "X", Headline = "Y" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateMe_ValidBody_Returns200WithResult()
    {
        var result = new UpdateUserProfileResult(TestAuthHandler.DefaultUserId, "Acme Ltd");

        _factory.MediatorSubstitute
            .Send(Arg.Any<UpdateUserProfileCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _authenticatedClient.PutAsJsonAsync("/users/me", new
        {
            FullName     = "Alice Smith",
            Headline     = "Engineer",
            EmployerName = "Acme Ltd",
            City         = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<UpdateUserProfileResult>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(TestAuthHandler.DefaultUserId);
    }

    // ── POST /users/me/resume ────────────────────────────────────────────────

    [Fact]
    public async Task SetResume_Unauthenticated_Returns401()
    {
        var response = await _anonymousClient.PostAsJsonAsync("/users/me/resume",
            new { FileName = "cv.pdf", SizeBytes = 102400 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SetResume_ValidBody_Returns200WithUploadUrl()
    {
        var result = new SetResumeResult(
            "https://s3.example.com/presigned-upload",
            "resumes/test-user/cv.pdf");

        _factory.MediatorSubstitute
            .Send(Arg.Any<SetResumeCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _authenticatedClient.PostAsJsonAsync("/users/me/resume", new
        {
            FileName  = "cv.pdf",
            SizeBytes = 102400L
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SetResumeResult>();
        body.Should().NotBeNull();
        body!.UploadUrl.Should().StartWith("https://");
    }
}
