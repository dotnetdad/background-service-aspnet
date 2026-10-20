using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.TokenInSave;

// Анти-пример раздела 6: стоппинг-токен в сохранении отметки — рассылка раздела 5.
// Включается Mistakes__TokenInSave=true и подменяет рассылку.
// Деплой посреди письма: письмо уходит (у отправки свой таймаут), а сохранение отметки
// с отменённым токеном бросает исключение отмены — отметки нет, и новый процесс
// пошлёт это письмо второй раз (README, 06-shutdown).
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
            try
            {
                await mail.SendAsync(p.To, p.Order);
                p.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                ScheduleRetry(p, ex);
            }
            await db.SaveChangesAsync(ct);
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
