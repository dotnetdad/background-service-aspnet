using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Background;

// Рассылка: раз в пять секунд берёт неотправленные письма из таблицы исходящих,
// шлёт и ставит отметку. Хост запускает её на старте и останавливает вместе с магазином.
// Ошибка почты остаётся в письме, ошибка прохода — в проходе: магазин живёт.
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
        var batch = await db.PendingEmails
            .Include(p => p.Order)
            .ThenInclude(o => o.Items)
            .Where(p => p.SentAt == null)
            .OrderBy(p => p.CreatedAt)
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
                log.LogError(ex,
                    "Письмо к заказу {OrderId} не ушло",
                    p.OrderId);
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
