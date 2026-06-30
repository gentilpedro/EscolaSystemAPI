namespace EscolaSystemApi.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy"] =
            "default-src 'none'; script-src 'self'; connect-src 'self'; " +
            "img-src 'self' data:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none';";

        if (!context.Request.IsHttps)
            headers["Strict-Transport-Security"] = "max-age=0";
        else
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        await next(context);
    }
}
