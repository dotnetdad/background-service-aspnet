using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.SelectNoLock;

// Анти-пример раздела 7: выборка LINQ без блокировки — рассылка раздела 6.
// Включается Mistakes__SelectNoLock=true и подменяет рассылку.
// Выборка только читает: два инстанса в одну секунду выбирают одни и те же
// двадцать писем, и покупатели получают по два письма (README, 07-instances).
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
        var batch = await db.PendingEmails
            .Include(p => p.Order)
            .ThenInclude(o => o.Items)
            .Where(p => p.SentAt == null
                     && p.Attempts < PendingEmail.MaxAttempts
                     && p.NextTryAt <= now)
            .OrderBy(p => p.NextTryAt)
            .Take(BatchSize)
            .ToListAsync(ct);
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
