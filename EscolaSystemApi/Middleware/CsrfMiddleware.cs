using System.Security.Cryptography;
using System.Text;
using EscolaSystemApi.Infrastructure.Security;

namespace EscolaSystemApi.Middleware;

// Double submit: o navegador manda os cookies sozinho, inclusive em requisições disparadas por outro site.
// Quem altera dados com a sessão em cookie precisa repetir o valor do cookie es_csrf no cabeçalho X-CSRF-Token,
// coisa que só o JavaScript do próprio front consegue fazer.
public class CsrfMiddleware(RequestDelegate next)
{
    private static readonly string[] UnsafeMethods = [HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete];

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresCsrf(context.Request) && !HasValidToken(context.Request))
        {
            const string message = "Requisição sem o token de segurança (CSRF). Recarregue a página e tente de novo.";
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = message, message });
            return;
        }

        await next(context);
    }

    private static bool RequiresCsrf(HttpRequest request) =>
        UnsafeMethods.Contains(request.Method)
        // Cabeçalho Authorization (ferramentas, integrações) não é enviado sozinho pelo navegador
        && !request.Headers.ContainsKey("Authorization")
        && (request.Cookies.ContainsKey(AuthCookies.AccessCookie) || request.Cookies.ContainsKey(AuthCookies.RefreshCookie))
        // No login ainda não há sessão a proteger, e é ele que emite o token de CSRF
        && !request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase);

    private static bool HasValidToken(HttpRequest request)
    {
        var cookie = request.Cookies[AuthCookies.CsrfCookie];
        var header = request.Headers[AuthCookies.CsrfHeader].ToString();
        return !string.IsNullOrEmpty(cookie)
               && !string.IsNullOrEmpty(header)
               && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header));
    }
}
