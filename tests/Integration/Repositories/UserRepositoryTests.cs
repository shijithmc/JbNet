using FluentAssertions;
using JbNet.Domain.Aggregates.Users;
using JbNet.Domain.Repositories;
using JbNet.Domain.ValueObjects;
using JbNet.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace JbNet.Tests.Integration.Repositories;

/// <summary>
/// Integration tests for <see cref="IUserRepository"/> backed by DynamoDB Local.
/// </summary>
public sealed class UserRepositoryTests : IClassFixture<DynamoDbFixture>
{
    private readonly IUserRepository _repo;
    private static readonly DateTimeOffset Now = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public UserRepositoryTests(DynamoDbFixture fixture)
    {
        _repo = fixture.Services.GetRequiredService<IUserRepository>();
    }

    [Fact]
    public async Task SaveAsync_NewUser_GetByIdAsync_ReturnsUser()
    {
        // Arrange
        var id   = UserId.From("u-int-001");
        var user = User.Create(id, "Alice Test", "alice-int@example.com", Now);

        // Act
        await _repo.SaveAsync(user, CancellationToken.None);
        var retrieved = await _repo.GetByIdAsync(id, CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Value.Should().Be("u-int-001");
        retrieved.FullName.Should().Be("Alice Test");
        retrieved.Email.Should().Be("alice-int@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentUser_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(UserId.From("u-does-not-exist-99999"), CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_ExistingUser_UpdatesProfile()
    {
        // Arrange
        var id   = UserId.From("u-int-002");
        var user = User.Create(id, "Bob Initial", "bob-int@example.com", Now);
        await _repo.SaveAsync(user, CancellationToken.None);

        // Act — mutate and save again
        user.UpdateProfile("Bob Updated", "Senior Engineer", "Acme Corp", "London", Now.AddDays(1));
        await _repo.SaveAsync(user, CancellationToken.None);
        var retrieved = await _repo.GetByIdAsync(id, CancellationToken.None);

        // Assert
        retrieved!.FullName.Should().Be("Bob Updated");
        retrieved.Headline.Should().Be("Senior Engineer");
        retrieved.EmployerName.Should().Be("Acme Corp");
    }

    [Fact]
    public async Task GetByEmailAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        var id   = UserId.From("u-int-003");
        var user = User.Create(id, "Carol Email", "carol-int@example.com", Now);
        await _repo.SaveAsync(user, CancellationToken.None);

        // Act
        var retrieved = await _repo.GetByEmailAsync("carol-int@example.com", CancellationToken.None);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Value.Should().Be("u-int-003");
    }

    [Fact]
    public async Task GetByEmailAsync_NonExistentEmail_ReturnsNull()
    {
        var result = await _repo.GetByEmailAsync("nobody-int@example.com", CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetResume_SaveAsync_GetByIdAsync_PersistsResumeKey()
    {
        // Arrange
        var id   = UserId.From("u-int-004");
        var user = User.Create(id, "Dana Resume", "dana-int@example.com", Now);
        await _repo.SaveAsync(user, CancellationToken.None);

        // Act
        user.SetResume("resumes/u-int-004/cv.pdf", "cv.pdf", 150_000, Now.AddMinutes(5));
        await _repo.SaveAsync(user, CancellationToken.None);
        var retrieved = await _repo.GetByIdAsync(id, CancellationToken.None);

        // Assert
        retrieved!.ResumeS3Key.Should().Be("resumes/u-int-004/cv.pdf");
        retrieved.ResumeFileName.Should().Be("cv.pdf");
        retrieved.ResumeSizeBytes.Should().Be(150_000);
    }

    [Fact]
    public async Task GetByIdsAsync_MultipleIds_ReturnsBatch()
    {
        // Arrange
        var id5  = UserId.From("u-int-005");
        var id6  = UserId.From("u-int-006");
        await _repo.SaveAsync(User.Create(id5, "Eli Batch", "eli-int@example.com",  Now), CancellationToken.None);
        await _repo.SaveAsync(User.Create(id6, "Fay Batch", "fay-int@example.com",  Now), CancellationToken.None);

        // Act
        var results = await _repo.GetByIdsAsync(new[] { id5, id6 }, CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        results.Select(u => u.Id.Value).Should().BeEquivalentTo(new[] { "u-int-005", "u-int-006" });
    }
}
