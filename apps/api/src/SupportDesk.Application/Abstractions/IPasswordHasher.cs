namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Hashes and verifies agent passwords using a secure salted hashing algorithm.
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string hashedPassword, string providedPassword);
}
