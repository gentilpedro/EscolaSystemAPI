using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EscolaSystemApi.Extensions;

public static class ObservabilityExtensions
{
    // Rotas de infraestrutura que não interessam nos traces (sondas e documentação)
    private static readonly string[] IgnoredPaths = ["/health", "/openapi", "/scalar"];

    /// <summary>
    /// Traces (HTTP e PostgreSQL) e métricas (HTTP e runtime) exportados por OTLP.
    /// Desligado por padrão: só liga com OpenTelemetry:OtlpEndpoint ou OTEL_EXPORTER_OTLP_ENDPOINT.
    /// </summary>
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var endpoint = FirstNonEmpty(configuration["OpenTelemetry:OtlpEndpoint"], configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (endpoint is null)
            return services;

        var protocol = string.Equals(configuration["OpenTelemetry:OtlpProtocol"], "http/protobuf", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.HttpProtobuf
            : OtlpExportProtocol.Grpc;
        var serviceName = FirstNonEmpty(configuration["OpenTelemetry:ServiceName"]) ?? "EscolaSystemApi";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString())
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = context => !IgnoredPaths.Any(p => context.Request.Path.StartsWithSegments(p));
                })
                // Instrumentação do próprio Npgsql: comando SQL e duração, sem os valores dos parâmetros
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation())
            .UseOtlpExporter(protocol, new Uri(endpoint));

        return services;
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
