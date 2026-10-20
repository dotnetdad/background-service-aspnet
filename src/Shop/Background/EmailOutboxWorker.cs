using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Background;

// Рассылка: раз в пять секунд берёт неотправленные письма из таблицы исходящих,
// шлёт и ставит отметку. Хост запускает её на старте и останавливает вместе с магазином.
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
        // свой scope на проход: у фона нет запроса
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
