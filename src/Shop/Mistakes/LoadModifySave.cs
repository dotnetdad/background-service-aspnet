using Microsoft.EntityFrameworkCore;
using Shop.Data;

namespace Shop.Mistakes.LoadModifySave;

// Анти-пример к разделу 11 («Дальше»): очистка, написанная загрузкой и сохранением.
// Включается Mistakes__LoadModifySave=true и подменяет очистку.
// Проигрывает гонку наоборот: загрузила просроченные заказы, покупатель в эту секунду
// оплатил, а SaveChanges пишет Cancelled по номеру и оплату перезаписывает.
// Окно растягивает POST /chaos/cleanup/slow (README, 11-race).
public class OrderCleanupWorker(
    IServiceScopeFactory scopes,
    ILogger<OrderCleanupWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(1));
        while (await timer
                   .WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CancelUnpaidAsync(stoppingToken);
            }
            catch (Exception ex)
                when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Очистка заказов упала");
            }
        }
    }

    // загрузить, поменять, сохранить
    private async Task CancelUnpaidAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AppDb>();
        var deadline = DateTime.UtcNow.AddMinutes(-30);
        var unpaid = await db.Orders
            .Where(o => o.Status == OrderStatus.New
                     && o.CreatedAt < deadline)
            .ToListAsync(ct);
        foreach (var order in unpaid)
            order.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        if (unpaid.Count > 0)
            log.LogInformation(
                "Отменено заказов: {Count}", unpaid.Count);
    }
}
