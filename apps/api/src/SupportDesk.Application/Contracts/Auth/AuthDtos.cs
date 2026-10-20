namespace SupportDesk.Application.Contracts.Auth;

/// <summary>
/// Request payload for POST /api/auth/login.
/// </summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// Authenticated agent identity.
/// </summary>
public sealed record CurrentUserDto(int Id, string FullName, string Email);

/// <summary>
/// Response payload for POST /api/auth/login.
/// </summary>
public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, CurrentUserDto User);
