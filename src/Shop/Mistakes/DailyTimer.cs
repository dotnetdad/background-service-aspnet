using Microsoft.EntityFrameworkCore;
using Shop.Data;

namespace Shop.Mistakes.DailyTimer;

// Анти-пример раздела 12: отчёт на PeriodicTimer раз в сутки.
// Включается Mistakes__DailyTimer=true и подменяет отчёт.
// Таймер считает сутки от старта процесса: деплой в 15:20 — отчёт в 15:20 завтра,
// а не в три ночи; у второго инстанса — свои сутки и свой отчёт за тот же день;
// деплой за минуту до отчёта начинает сутки заново, и отчёт за день не придёт.
// Время первого отчёта анти-пример пишет в лог на старте (README, 12-nightly).
public class DailySalesWorker(
    IServiceScopeFactory scopes,
    IConfiguration config,
    ILogger<DailySalesWorker> log) : BackgroundService
{
    private readonly TimeZoneInfo zone = TimeZoneInfo
        .FindSystemTimeZoneById(config["Shop:TimeZone"] ?? "Europe/Moscow");

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        log.LogInformation(
            "Первый отчёт — через сутки после старта: {At:dd.MM HH:mm} по времени магазина",
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow.AddDays(1), zone));
        // Анти-пример: сутки от старта процесса
        using var timer = new PeriodicTimer(
            TimeSpan.FromDays(1));
        while (await timer
                   .WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SaveDailySalesAsync(stoppingToken);
            }
            catch (Exception ex)
                when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Отчёт о продажах упал");
            }
        }
    }

    private async Task SaveDailySalesAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AppDb>();
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone).Date;
        var from = TimeZoneInfo.ConvertTimeToUtc(today.AddDays(-1), zone);
        var to = TimeZoneInfo.ConvertTimeToUtc(today, zone);
        var paid = db.Orders.Where(o => o.Status == OrderStatus.Paid
                                     && o.CreatedAt >= from && o.CreatedAt < to);
        var orders = await paid.CountAsync(ct);
        var revenue = await paid.SelectMany(o => o.Items)
            .SumAsync(i => i.Price * i.Quantity, ct);
        db.DailySales.Add(new DailySale(
            DateOnly.FromDateTime(today.AddDays(-1)), orders, revenue));
        await db.SaveChangesAsync(ct);
    }
}
