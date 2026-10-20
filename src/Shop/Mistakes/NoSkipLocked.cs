using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.NoSkipLocked;

// Анти-пример раздела 7: команда «забрать батч» без FOR UPDATE SKIP LOCKED.
// Включается Mistakes__NoSkipLocked=true и подменяет рассылку; нужны два инстанса.
// Подзапросы A и B в одну секунду выбирают одни и те же строки. B упирается в строки,
// которые меняет A, и ждёт; A закончил — и B обновляет те же строки ещё раз:
// выбор своего подзапроса после ожидания он не пересчитывает. Оба шлют один батч.
// В жизни окно — миллисекунды, пока идёт команда. Здесь команда держит строки
// три секунды в транзакции, чтобы дубль ловился без везения (README, 07-instances).
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
        // окно растянуто: команда держит строки три секунды
        await using var tx =
            await db.Database.BeginTransactionAsync(ct);
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
                    ORDER BY "NextTryAt" LIMIT {BatchSize})
                RETURNING *
                """)
            .ToListAsync(ct);
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        await tx.CommitAsync(ct);
        await tx.DisposeAsync();
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
