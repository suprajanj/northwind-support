namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when authentication fails or credentials are rejected. Surfaces as HTTP 401.
/// </summary>
public sealed class UnauthorizedException(string message) : Exception(message);
