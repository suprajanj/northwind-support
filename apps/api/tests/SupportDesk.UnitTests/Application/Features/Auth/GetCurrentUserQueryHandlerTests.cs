using Moq;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.GetCurrentUser;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using Xunit;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public sealed class GetCurrentUserQueryHandlerTests
{
    private readonly Mock<IAgentRepository> _agentsMock = new();
    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryHandlerTests()
    {
        _handler = new GetCurrentUserQueryHandler(_agentsMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenAgentExistsAndActive_ReturnsCurrentUserDto()
    {
        var agent = new Agent("Alex Turner", "alex.turner@northwind-support.example", 10, DateTime.UtcNow, "hash");
        _agentsMock.Setup(r => r.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var result = await _handler.HandleAsync(agent.Id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(agent.Id, result.Id);
        Assert.Equal(agent.FullName, result.FullName);
        Assert.Equal(agent.Email, result.Email);
    }

    [Fact]
    public async Task HandleAsync_WhenAgentNotFound_ThrowsUnauthorizedException()
    {
        _agentsMock.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WhenAgentIsInactive_ThrowsUnauthorizedException()
    {
        var agent = new Agent("Inactive Agent", "inactive@northwind-support.example", 10, DateTime.UtcNow, "hash");
        agent.Deactivate();

        _agentsMock.Setup(r => r.GetByIdAsync(agent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _handler.HandleAsync(agent.Id, CancellationToken.None));
    }
}
