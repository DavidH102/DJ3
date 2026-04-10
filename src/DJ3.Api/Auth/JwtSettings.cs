namespace DJ3.Api.Auth;

public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "DJ3.Api";
    public string Audience { get; set; } = "DJ3.Api.Clients";
    public int ExpirationMinutes { get; set; } = 60;
}
