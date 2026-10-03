using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace EscolaSystemApi.Middleware;

// Mantém o comportamento padrão (status e WWW-Authenticate) e acrescenta o corpo com a mensagem,
// que o [Authorize] sozinho devolve vazio
public class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        await _default.HandleAsync(next, context, policy, authorizeResult);

        if (context.Response.HasStarted)
            return;

        if (authorizeResult.Challenged)
            await ApiErrors.WriteAsync(context, ApiErrors.Unauthorized);
        else if (authorizeResult.Forbidden)
            await ApiErrors.WriteAsync(context, ApiErrors.Forbidden);
    }
}
