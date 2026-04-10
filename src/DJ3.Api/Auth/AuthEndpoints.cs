using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DJ3.Api.Auth;

public static class AuthEndpoints
{
    public record TokenRequest(string Username, string Role);

    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v2/auth/token", (TokenRequest request, IOptions<JwtSettings> jwtOptions) =>
        {
            var settings = jwtOptions.Value;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddMinutes(settings.ExpirationMinutes);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, request.Username),
                new Claim(ClaimTypes.Role, request.Role),
                new Claim(JwtRegisteredClaimNames.UniqueName, request.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: settings.Issuer,
                audience: settings.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Results.Ok(new { token = tokenString, expiresAt });
        });

        return app;
    }
}
