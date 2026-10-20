using Microsoft.EntityFrameworkCore;
using Shop.Background;
using Shop.Data;

namespace Shop.Mistakes.TimerCleanup;

// Анти-пример раздела 10: очистка на System.Threading.Timer с асинхронной лямбдой.
// Включается Mistakes__TimerCleanup=true и подменяет очистку.
// Колбэк Timer возвращает void, асинхронная лямбда в нём — async void: таски нет,
// и Timer не ждёт её конца. Останови базу (docker compose stop db): исключение
// после await некуда положить, оно брошено в пул потоков и роняет весь процесс —
// вместе с рассылкой и приёмом заказов (README, 10-schedule).
public class OrderCleanupWorker(
    IServiceScopeFactory scopes,
    CleanupStatus status,
    ILogger<OrderCleanupWorker> log) : IHostedService, IDisposable
{
    private Timer? _timer;

    // Анти-пример: Timer с асинхронной лямбдой
    public Task StartAsync(CancellationToken ct)
    {
        _timer = new Timer(
            async _ => await CancelUnpaidAsync(
                CancellationToken.None),
            null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private async Task CancelUnpaidAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AppDb>();
        var deadline = DateTime.UtcNow.AddMinutes(-30);
        var cancelled = await db.Orders
            .Where(o => o.Status == OrderStatus.New
                     && o.CreatedAt < deadline)
            .ExecuteUpdateAsync(s => s.SetProperty(
                o => o.Status, OrderStatus.Cancelled), ct);
        if (cancelled > 0)
            log.LogInformation(
                "Отменено заказов: {Count}", cancelled);
        status.Passed(); // для хелсчека, как у настоящей очистки
    }
}
