namespace SupportDesk.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";

    public string SecretKey { get; init; } = "LocalDev_Jwt_Secret_Key_Change_In_Production_32chars!";

    public string Issuer { get; init; } = "NorthwindSupportDesk";

    public string Audience { get; init; } = "NorthwindSupportDesk";

    public int ExpiryHours { get; init; } = 8;
}
