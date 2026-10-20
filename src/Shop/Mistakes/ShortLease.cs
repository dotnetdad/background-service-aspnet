using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.ShortLease;

// Анти-пример раздела 8: аренда тридцать секунд — короче батча.
// Включается Mistakes__ShortLease=true и подменяет рассылку; нужны два инстанса.
// Почта отвечает по девять секунд (POST /chaos/mail/slow), батч из двадцати писем
// идёт три минуты, а аренда вышла через тридцать секунд: свободный инстанс забирает
// письма, которые первый ещё шлёт, и дальше оба шлют одни и те же (README, 08-lease).
public class EmailOutboxWorker(
    IServiceScopeFactory scopes,
    IOrderMail mail,
    ILogger<EmailOutboxWorker> log) : BackgroundService
{
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(5));
        while (await timer
                   .WaitForNextTickAsync(stoppingToken))
        {
            // отмену при остановке catch пропускает
            try
            {
                await SendBatchAsync(stoppingToken);
            }
            catch (Exception ex)
                when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Проход рассылки упал");
            }
        }
    }

    private async Task SendBatchAsync(CancellationToken ct)
    {
        // свой scope на проход: у фона нет запроса
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AppDb>();
        var now = DateTime.UtcNow;
        // Анти-пример: аренда короче батча
        var lease = now.AddSeconds(30);
        var batch = await db.PendingEmails
            .FromSql($"""
                UPDATE "PendingEmails"
                SET "NextTryAt" = {lease}
                WHERE "Id" IN (
                    SELECT "Id" FROM "PendingEmails"
                    WHERE "SentAt" IS NULL
                    AND "Attempts" < {PendingEmail.MaxAttempts}
                    AND "NextTryAt" <= {now}
                    ORDER BY "NextTryAt" LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .ToListAsync(ct);
        if (batch.Count == 0)
            return;
        // заказы батча — отдельным запросом:
        // к UPDATE … RETURNING Include не прицепить
        var ids = batch.Select(p => p.OrderId).ToList();
        await db.Orders.Include(o => o.Items)
            .Where(o => ids.Contains(o.Id))
            .LoadAsync(ct);
        foreach (var p in batch)
        {
            // остановка — новое письмо не начинаем
            if (ct.IsCancellationRequested)
                break;
            try
            {
                await mail.SendAsync(p.To, p.Order);
                p.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                ScheduleRetry(p, ex);
            }
            // отметку сохраняем и при остановке
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    // ошибка почты: попытка, пауза, строка в лог
    private void ScheduleRetry(PendingEmail p, Exception ex)
    {
        p.Attempts++;
        // пауза 1, 4, 9, 16 минут
        p.NextTryAt = DateTime.UtcNow
            .AddMinutes(p.Attempts * p.Attempts);
        // пятая ошибка — письмо сдалось: это уже Error
        log.Log(p.Attempts < PendingEmail.MaxAttempts
                ? LogLevel.Warning : LogLevel.Error,
            ex, "Письмо к заказу {OrderId} не ушло",
            p.OrderId);
    }
}
