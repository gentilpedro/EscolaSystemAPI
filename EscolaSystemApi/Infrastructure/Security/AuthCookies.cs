using System.Security.Cryptography;
using EscolaSystemApi.Application.DTOs.Auth;

namespace EscolaSystemApi.Infrastructure.Security;

// Onde os tokens moram no navegador: cookies que o JavaScript da página não consegue ler,
// mais o cookie de CSRF, que o front lê e devolve no cabeçalho X-CSRF-Token
public class AuthCookies(IConfiguration configuration, IHostEnvironment environment)
{
    public const string AccessCookie = "es_access";
    public const string RefreshCookie = "es_refresh";
    public const string CsrfCookie = "es_csrf";
    public const string CsrfHeader = "X-CSRF-Token";
    // O refresh token só viaja para /api/auth (renovação e saída), nunca nas demais requisições
    public const string RefreshPath = "/api/auth";

    // Em Development (http://localhost) o cookie não pode exigir HTTPS
    private readonly bool _secure = configuration.GetValue("Auth:SecureCookies", !environment.IsDevelopment());
    private readonly string? _domain = string.IsNullOrWhiteSpace(configuration["Auth:CookieDomain"]) ? null : configuration["Auth:CookieDomain"];

    public void Write(HttpRequest request, HttpResponse response, AuthSession session, bool newCsrfToken)
    {
        response.Cookies.Append(AccessCookie, session.AccessToken, Options(httpOnly: true, "/", session.AccessExpiresAt));
        response.Cookies.Append(RefreshCookie, session.RefreshToken, Options(httpOnly: true, RefreshPath, session.RefreshExpiresAt));

        // Na renovação o token de CSRF continua o mesmo: requisições em andamento não falham por causa dela
        var csrf = !newCsrfToken && request.Cookies.TryGetValue(CsrfCookie, out var existing) && !string.IsNullOrEmpty(existing)
            ? existing
            : Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        response.Cookies.Append(CsrfCookie, csrf, Options(httpOnly: false, "/", session.RefreshExpiresAt));
    }

    public void Clear(HttpResponse response)
    {
        response.Cookies.Delete(AccessCookie, Options(httpOnly: true, "/", null));
        response.Cookies.Delete(RefreshCookie, Options(httpOnly: true, RefreshPath, null));
        response.Cookies.Delete(CsrfCookie, Options(httpOnly: false, "/", null));
    }

    private CookieOptions Options(bool httpOnly, string path, DateTime? expiresUtc) => new()
    {
        HttpOnly = httpOnly,
        Secure = _secure,
        SameSite = SameSiteMode.Strict,
        Path = path,
        Domain = _domain,
        Expires = expiresUtc is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expiresUtc.Value, DateTimeKind.Utc)),
        IsEssential = true
    };
}
