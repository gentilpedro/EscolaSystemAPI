using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EscolaSystemApi.Common;

// Formato único de erro que o front lê: "message" para exibir, "error" por compatibilidade
public static class ApiErrors
{
    public const string Unauthorized = "Sessão expirada ou inválida. Faça login novamente.";
    public const string Forbidden = "Você não tem permissão para esta ação.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, string message)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message, message }, JsonOptions));
    }

    // Erros de binding (JSON malformado, Guid ou data inválidos) no mesmo formato dos validators
    public static IActionResult InvalidModelState(ActionContext context) =>
        new BadRequestObjectResult(BuildInvalidModelState(context.ModelState));

    public static object BuildInvalidModelState(ModelStateDictionary modelState)
    {
        var fields = modelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .Select(entry => entry.Key.TrimStart('$', '.'))
            .Where(field => field.Length > 0)
            .Distinct()
            .ToList();

        var message = fields.Count == 0
            ? "O corpo da requisição está ausente ou não é um JSON válido."
            : $"Valor inválido no(s) campo(s): {string.Join(", ", fields)}.";

        var errors = modelState
            .SelectMany(entry => entry.Value?.Errors.Select(e => e.ErrorMessage) ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToList();

        return new { error = message, message, errors };
    }
}
