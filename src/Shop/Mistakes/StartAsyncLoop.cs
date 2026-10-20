using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.StartAsyncLoop;

// Анти-пример к разделу 3 («Дальше»): свой IHostedService с циклом прямо в StartAsync.
// Включается Mistakes__StartAsyncLoop=true и подменяет рассылку.
// Хост ждёт StartAsync каждого сервиса и только потом поднимает веб-сервер.
// Этот StartAsync не вернётся никогда: письма уходят, а магазин не принимает
// запросы, строки «Now listening» в логе нет, ошибки тоже (README, 03-worker).
public class EmailOutboxWorker(
    IServiceScopeFactory scopes,
    IOrderMail mail) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(ct))
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
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
