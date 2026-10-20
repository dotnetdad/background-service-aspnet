using Microsoft.EntityFrameworkCore;
using Shop.Data;

namespace Shop.Background;

// Очистка: раз в минуту отменяет заказы, которые не оплатили за полчаса, —
// всех разом, одной командой в базе. Цикл тот же, что у рассылки.
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

    private async Task CancelUnpaidAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AppDb>();
        var deadline = DateTime.UtcNow.AddMinutes(-30);
        // все просроченные — одной командой в базе
        var cancelled = await db.Orders
            .Where(o => o.Status == OrderStatus.New
                     && o.CreatedAt < deadline)
            .ExecuteUpdateAsync(s => s.SetProperty(
                o => o.Status, OrderStatus.Cancelled), ct);
        if (cancelled > 0)
            log.LogInformation(
                "Отменено заказов: {Count}", cancelled);
    }
}
