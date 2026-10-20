using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.NoTry;

// Анти-пример раздела 4: рассылка без try — та, что была на теге раздела 3.
// Включается Mistakes__NoTry=true и подменяет рассылку.
// Попробуй POST /chaos/mail/fail-next и заказ: SmtpException выходит из ExecuteAsync,
// хост (BackgroundServiceExceptionBehavior.StopHost) останавливает весь магазин,
// процесс выходит с кодом 0, и restart: on-failure его не поднимает (README, 04-try).
public class EmailOutboxWorker(
    IServiceScopeFactory scopes,
    IOrderMail mail) : BackgroundService
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
            await SendBatchAsync(stoppingToken);
        }
    }

    private async Task SendBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AppDb>();
        var batch = await db.PendingEmails
            .Include(p => p.Order)
            .ThenInclude(o => o.Items)
            .Where(p => p.SentAt == null)
            .OrderBy(p => p.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);
        foreach (var p in batch)
        {
            await mail.SendAsync(p.To, p.Order);
            p.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
