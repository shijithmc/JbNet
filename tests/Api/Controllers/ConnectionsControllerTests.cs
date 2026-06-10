using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JbNet.Application.Connections.Commands.AcceptConnectionRequest;
using JbNet.Application.Connections.Commands.SendConnectionRequest;
using JbNet.Tests.Api.Infrastructure;
using MediatR;
using NSubstitute;

namespace JbNet.Tests.Api.Controllers;

/// <summary>
/// API contract tests for <c>/connections</c> endpoints.
/// MediatR <see cref="ISender"/> is substituted — no real infrastructure required.
/// </summary>
public sealed class ConnectionsControllerTests : IClassFixture<JbNetApiFactory>, IDisposable
{
    private readonly JbNetApiFactory _factory;
    private readonly HttpClient _authenticatedClient;
    private readonly HttpClient _anonymousClient;

    public ConnectionsControllerTests(JbNetApiFactory factory)
    {
        _factory             = factory;
        _authenticatedClient = factory.CreateClient();
        _authenticatedClient.DefaultRequestHeaders.Add("Authorization", TestAuthHandler.FakeBearer);

        _anonymousClient = factory.CreateClient();
    }

    public void Dispose()
    {
        _authenticatedClient.Dispose();
        _anonymousClient.Dispose();
    }

    // ── POST /connections ────────────────────────────────────────────────────

    [Fact]
    public async Task SendConnection_Unauthenticated_Returns401()
    {
        var response = await _anonymousClient.PostAsJsonAsync("/connections",
            new { TargetUserId = "other-user", Note = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SendConnection_ValidRequest_Returns201WithConnectionId()
    {
        var result = new SendConnectionRequestResult("conn-abc-001");

        _factory.MediatorSubstitute
            .Send(Arg.Any<SendConnectionRequestCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _authenticatedClient.PostAsJsonAsync("/connections", new
        {
            TargetUserId = "target-user-sub-002",
            Note         = "We met at a conference"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<SendConnectionRequestResult>();
        body.Should().NotBeNull();
        body!.ConnectionId.Should().Be("conn-abc-001");
    }

    [Fact]
    public async Task SendConnection_CommandReceivesCorrectRequesterId()
    {
        _factory.MediatorSubstitute
            .Send(Arg.Any<SendConnectionRequestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new SendConnectionRequestResult("conn-check-001"));

        await _authenticatedClient.PostAsJsonAsync("/connections", new
        {
            TargetUserId = "target-user-sub-002",
            Note         = (string?)null
        });

        // Verify the controller wired the current user's sub claim as RequesterId
        await _factory.MediatorSubstitute.Received(1)
            .Send(
                Arg.Is<SendConnectionRequestCommand>(c =>
                    c.RequesterId == TestAuthHandler.DefaultUserId &&
                    c.TargetUserId == "target-user-sub-002"),
                Arg.Any<CancellationToken>());
    }

    // ── POST /connections/{requesterId}/accept ───────────────────────────────

    [Fact]
    public async Task AcceptConnection_Unauthenticated_Returns401()
    {
        var response = await _anonymousClient.PostAsync(
            "/connections/some-requester-id/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AcceptConnection_ValidRequest_Returns200WithStatus()
    {
        var result = new AcceptConnectionRequestResult("Accepted");

        _factory.MediatorSubstitute
            .Send(Arg.Any<AcceptConnectionRequestCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var response = await _authenticatedClient.PostAsync(
            "/connections/requester-user-sub-003/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AcceptConnectionRequestResult>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task AcceptConnection_CommandReceivesCorrectAccepterId()
    {
        _factory.MediatorSubstitute
            .Send(Arg.Any<AcceptConnectionRequestCommand>(), Arg.Any<CancellationToken>())
            .Returns(new AcceptConnectionRequestResult("Accepted"));

        await _authenticatedClient.PostAsync(
            "/connections/requester-user-sub-003/accept", null);

        await _factory.MediatorSubstitute.Received(1)
            .Send(
                Arg.Is<AcceptConnectionRequestCommand>(c =>
                    c.AccepterId  == TestAuthHandler.DefaultUserId &&
                    c.RequesterId == "requester-user-sub-003"),
                Arg.Any<CancellationToken>());
    }
}
