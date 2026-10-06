using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (status, message) = exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Não autenticado."),
            // Alguém alterou ou excluiu o registro entre a leitura e a gravação
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict,
                "O registro foi alterado ou excluído por outra pessoa. Recarregue e tente de novo."),
            // Violação de FK/índice único que escapou das validações do service
            DbUpdateException => (HttpStatusCode.Conflict,
                "A operação conflita com registros existentes (duplicidade ou vínculos dependentes)."),
            _ => (HttpStatusCode.InternalServerError, "Ocorreu um erro interno. Tente novamente mais tarde.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        var response = new
        {
            error = message,
            message,
            traceId = context.TraceIdentifier
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
