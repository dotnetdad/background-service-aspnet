using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.ExitingLoop;

// Анти-пример к разделу 3 («Дальше»): цикл, который кончается сам.
// Включается Mistakes__ExitingLoop=true и подменяет рассылку.
// Цикл шлёт письма, «пока они есть». На старте таблица пуста: цикл сразу кончился,
// ExecuteAsync вернулся, и хост его заново не запустит. Заказы принимаются,
// письма не уходят, в логе ни ошибки, ни предупреждения (README, 03-worker).
public class EmailOutboxWorker(
    IServiceScopeFactory scopes,
    IOrderMail mail) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        int sent;
        do sent = await SendBatchAsync(stoppingToken);
        while (sent > 0);
    }

    private async Task<int> SendBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AppDb>();
        var batch = await db.PendingEmails
            .Include(p => p.Order)
            .ThenInclude(o => o.Items)
            .Where(p => p.SentAt == null)
            .OrderBy(p => p.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
        foreach (var p in batch)
        {
            await mail.SendAsync(p.To, p.Order);
            p.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return batch.Count;
    }
}
