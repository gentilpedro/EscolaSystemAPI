using EscolaSystemApi.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EscolaSystemApi.Infrastructure.HealthChecks;

// Prontidão: a API só está pronta para receber tráfego se alcança o banco
public class DatabaseHealthCheck(AppDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Banco de dados inacessível.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Banco de dados inacessível.", ex);
        }
    }
}
