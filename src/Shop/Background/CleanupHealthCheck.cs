using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shop.Background;

// Очистка проходит раз в минуту: три минуты без успешного прохода — она стоит
public class CleanupHealthCheck(CleanupStatus status) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        var idle = DateTime.UtcNow - status.LastPassAt;
        return Task.FromResult(idle < TimeSpan.FromMinutes(3)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded(
                $"Очистка стоит {(int)idle.TotalMinutes} мин"));
    }
}
