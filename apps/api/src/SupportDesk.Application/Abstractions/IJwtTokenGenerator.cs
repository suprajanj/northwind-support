namespace SupportDesk.Application.Abstractions;

public sealed record GeneratedToken(string Token, DateTime ExpiresAtUtc);

public interface IJwtTokenGenerator
{
    GeneratedToken GenerateToken(int agentId, string fullName, string email);
}
