using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using JbNet.Application.Connections.Queries.GetAcceptedConnections;
using JbNet.Tests.Api.Infrastructure;
using MediatR;
using NSubstitute;

namespace JbNet.Tests.Api.Controllers;

/// <summary>
/// Contract tests for <c>GET /connections/me</c>.
/// </summary>
public sealed class ConnectionsGetTests : IClassFixture<JbNetApiFactory>, IDisposable
{
    private readonly JbNetApiFactory _factory;
    private readonly HttpClient _authenticatedClient;
    private readonly HttpClient _anonymousClient;

    public ConnectionsGetTests(JbNetApiFactory factory)
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

    [Fact]
    public async Task GetMyConnections_Unauthenticated_Returns401()
    {
        var response = await _anonymousClient.GetAsync("/connections/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyConnections_NoConnections_Returns200WithEmptyList()
    {
        _factory.MediatorSubstitute
            .Send(Arg.Any<GetAcceptedConnectionsQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ConnectionSummary>)Array.Empty<ConnectionSummary>());

        var response = await _authenticatedClient.GetAsync("/connections/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ConnectionSummary>>();
        body.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetMyConnections_WithConnections_Returns200WithList()
    {
        var connections = new List<ConnectionSummary>
        {
            new("conn-001", "user-002", DateTimeOffset.UtcNow.AddDays(-10)),
            new("conn-002", "user-003", DateTimeOffset.UtcNow.AddDays(-5)),
        };

        _factory.MediatorSubstitute
            .Send(Arg.Any<GetAcceptedConnectionsQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ConnectionSummary>)connections);

        var response = await _authenticatedClient.GetAsync("/connections/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<ConnectionSummary>>();
        body.Should().NotBeNull().And.HaveCount(2);
        body![0].ConnectionId.Should().Be("conn-001");
        body[1].ConnectedUserId.Should().Be("user-003");
    }

    [Fact]
    public async Task GetMyConnections_QueryReceivesCorrectUserId()
    {
        _factory.MediatorSubstitute
            .Send(Arg.Any<GetAcceptedConnectionsQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ConnectionSummary>)Array.Empty<ConnectionSummary>());

        await _authenticatedClient.GetAsync("/connections/me");

        await _factory.MediatorSubstitute.Received(1)
            .Send(
                Arg.Is<GetAcceptedConnectionsQuery>(q =>
                    q.UserId == TestAuthHandler.DefaultUserId),
                Arg.Any<CancellationToken>());
    }
}
