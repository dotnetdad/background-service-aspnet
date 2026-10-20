using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Background;

// Рассылка: раз в пять секунд забирает себе батч писем, чьё время пришло, шлёт и ставит отметку.
// Забирает одной командой: FOR UPDATE SKIP LOCKED пропускает строки, которые сейчас
// забирает другой инстанс, а «взято до» (NextTryAt на пять минут вперёд) прячет взятые.
// Ошибка почты добавляет попытку и отодвигает следующую: 1, 4, 9, 16 минут.
// После пятой ошибки письмо выходит из выборки и ждёт разбора (OutboxController).
// На остановке начатое письмо уходит до конца и получает отметку, новое не начинается.
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
        // забрать батч себе одной командой: «взято до» —
        // время следующей попытки на пять минут вперёд
        var now = DateTime.UtcNow;
        var lease = now.AddMinutes(5);
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
