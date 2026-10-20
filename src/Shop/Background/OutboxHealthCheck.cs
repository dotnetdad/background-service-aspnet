using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shop.Data;

namespace Shop.Background;

// Хелсчек рассылки смотрит на сделанную работу, а не на процесс:
// самое старое неотправленное письмо ждёт больше десяти минут — Degraded;
// есть письма после пяти ошибок — Degraded, пока их не разберут.
// создаётся на каждую проверку, в своём scope
public class OutboxHealthCheck(AppDb db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        var max = PendingEmail.MaxAttempts;
        var oldest = await db.PendingEmails
            .Where(p => p.SentAt == null && p.Attempts < max)
            .MinAsync(p => (DateTime?)p.CreatedAt, ct);
        // держится, пока сдавшиеся письма не разобрали
        var givenUp = await db.PendingEmails
            .CountAsync(p => p.SentAt == null
                          && p.Attempts >= max, ct);
        if (givenUp > 0)
            return HealthCheckResult.Degraded(
                $"Писем после пяти попыток: {givenUp}");
        if (DateTime.UtcNow - oldest > TimeSpan.FromMinutes(10))
            return HealthCheckResult.Degraded(
                "Письмо не уходит больше десяти минут");
        return HealthCheckResult.Healthy();
    }
}
