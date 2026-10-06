using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EscolaSystemApi.Application.Services;

public class JwtService(IConfiguration configuration) : IJwtService
{
    public const string SessionClaim = "sid";

    private readonly string _key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "EscolaSystemApi";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "EscolaSystemApiClient";
    private readonly int _accessMinutes = configuration.GetValue("Jwt:AccessTokenMinutes", 15);
    private readonly int _refreshDays = configuration.GetValue("Jwt:RefreshTokenDays", 7);

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_refreshDays);

    public (string Token, DateTime ExpiresAt) GenerateToken(User user, Guid sessionId)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_accessMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role?.Name ?? "Unknown"),
            new Claim("schoolId", user.SchoolId?.ToString() ?? string.Empty),
            new Claim("studentId", user.StudentId?.ToString() ?? string.Empty),
            new Claim(SessionClaim, sessionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
