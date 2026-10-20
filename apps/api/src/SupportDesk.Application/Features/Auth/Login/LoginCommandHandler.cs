using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IAgentRepository agents,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator,
    ILogger<LoginCommandHandler> logger)
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var agent = await agents.GetByEmailAsync(request.Email, ct);

        if (agent is null)
        {
            logger.LogWarning("Login failed: no agent found with email {Email}.", request.Email);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (!passwordHasher.VerifyPassword(agent.PasswordHash, request.Password))
        {
            logger.LogWarning("Login failed: incorrect password for email {Email}.", request.Email);
            throw new UnauthorizedException(InvalidCredentialsMessage);
        }

        if (!agent.IsActive)
        {
            logger.LogWarning("Login failed: agent {Email} is inactive.", request.Email);
            throw new UnauthorizedException("Your account is inactive. Please contact your administrator.");
        }

        var token = tokenGenerator.GenerateToken(agent.Id, agent.FullName, agent.Email);

        logger.LogInformation("Agent {Email} (Id={AgentId}) successfully logged in.", agent.Email, agent.Id);

        return new LoginResponse(
            token.Token,
            token.ExpiresAtUtc,
            new CurrentUserDto(agent.Id, agent.FullName, agent.Email));
    }
}
