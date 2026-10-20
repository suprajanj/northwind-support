using SupportDesk.Infrastructure.Security;
using Xunit;

namespace SupportDesk.UnitTests.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ProducesNonEmptyHashWithExpectedFormat()
    {
        var password = "Password123!";

        var hash = _hasher.HashPassword(password);

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        var parts = hash.Split('$');
        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2", parts[0]);
    }

    [Fact]
    public void HashPassword_ProducesDifferentSaltsForSamePassword()
    {
        var password = "Password123!";

        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_ReturnsTrue_ForCorrectPassword()
    {
        var password = "CorrectHorseBatteryStaple!";
        var hash = _hasher.HashPassword(password);

        var result = _hasher.VerifyPassword(hash, password);

        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalse_ForWrongPassword()
    {
        var password = "CorrectPassword123!";
        var hash = _hasher.HashPassword(password);

        var result = _hasher.VerifyPassword(hash, "WrongPassword!");

        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-format")]
    [InlineData("PBKDF2$not-a-number$salt$key")]
    [InlineData("too$few$parts")]
    public void VerifyPassword_ReturnsFalse_ForMalformedHash(string malformedHash)
    {
        var result = _hasher.VerifyPassword(malformedHash, "Password123!");

        Assert.False(result);
    }
}
