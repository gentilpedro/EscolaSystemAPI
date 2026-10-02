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
    private readonly string _key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "EscolaSystemApi";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "EscolaSystemApiClient";
    private readonly int _expirationMinutes = int.Parse(configuration["Jwt:ExpirationInMinutes"] ?? "60");

    public (string Token, DateTime ExpiresAt) GenerateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_expirationMinutes);
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

    public Guid? GetUserIdFromToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token)) return null;

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
        catch
        {
            return null;
        }
    }
}
