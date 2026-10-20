using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using Xunit;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public sealed class LoginCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _agentsMock = new();
    private readonly Mock<IPasswordHasher> _hasherMock = new();
    private readonly Mock<IJwtTokenGenerator> _tokenGeneratorMock = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _agentsMock.Object,
            _hasherMock.Object,
            _tokenGeneratorMock.Object,
            NullLogger<LoginCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WithValidCredentials_ReturnsLoginResponse()
    {
        var email = "alex.turner@northwind-support.example";
        var password = "Password123!";
        var agent = new Agent("Alex Turner", email, 10, DateTime.UtcNow, "hashed_password");

        _agentsMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _hasherMock.Setup(h => h.VerifyPassword("hashed_password", password))
            .Returns(true);
        _tokenGeneratorMock.Setup(g => g.GenerateToken(agent.Id, agent.FullName, agent.Email))
            .Returns(new GeneratedToken("jwt-token-xyz", DateTime.UtcNow.AddHours(8)));

        var result = await _handler.HandleAsync(new LoginRequest(email, password), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("jwt-token-xyz", result.Token);
        Assert.Equal(agent.FullName, result.User.FullName);
        Assert.Equal(email, result.User.Email);
    }

    [Fact]
    public async Task HandleAsync_WhenAgentNotFound_ThrowsUnauthorizedException()
    {
        _agentsMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(new LoginRequest("nonexistent@example.com", "Password123!"), CancellationToken.None));

        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenPasswordDoesNotMatch_ThrowsUnauthorizedException()
    {
        var email = "alex.turner@northwind-support.example";
        var agent = new Agent("Alex Turner", email, 10, DateTime.UtcNow, "hashed_password");

        _agentsMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _hasherMock.Setup(h => h.VerifyPassword("hashed_password", "WrongPassword!"))
            .Returns(false);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(new LoginRequest(email, "WrongPassword!"), CancellationToken.None));

        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenAgentIsInactive_ThrowsUnauthorizedException()
    {
        var email = "inactive.agent@northwind-support.example";
        var agent = new Agent("Inactive Agent", email, 10, DateTime.UtcNow, "hashed_password");
        agent.Deactivate();

        _agentsMock.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
        _hasherMock.Setup(h => h.VerifyPassword("hashed_password", "Password123!"))
            .Returns(true);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(new LoginRequest(email, "Password123!"), CancellationToken.None));

        Assert.Contains("inactive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
