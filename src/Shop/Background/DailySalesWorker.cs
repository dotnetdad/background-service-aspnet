using Microsoft.EntityFrameworkCore;
using Shop.Data;

namespace Shop.Background;

// Отчёт о продажах за вчера — в три ночи по времени магазина и один раз на все инстансы.
// Время следующего запуска лежит строкой в таблице расписания. Раз в минуту каждый
// инстанс пробует сдвинуть его командой с условием «время пришло»: кто сдвинул,
// тот и считает. Сдвиг и запись отчёта — одна транзакция.
public class DailySalesWorker(
    IServiceScopeFactory scopes,
    IConfiguration config,
    ILogger<DailySalesWorker> log) : BackgroundService
{
    // пояс магазина — Shop:TimeZone в настройках, в проекте Москва
    private readonly TimeZoneInfo zone = TimeZoneInfo
        .FindSystemTimeZoneById(config["Shop:TimeZone"] ?? "Europe/Moscow");

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
                await RunIfDueAsync(stoppingToken);
            }
            catch (Exception ex)
                when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Отчёт о продажах упал");
            }
        }
    }

    private async Task RunIfDueAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AppDb>();
        var now = DateTime.UtcNow;
        var next = NextThreeAm(now, zone);
        await using var tx =
            await db.Database.BeginTransactionAsync(ct);
        var mine = await db.ScheduledJobs
            .Where(j => j.Name == "daily-sales"
                     && j.NextRunAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(
                j => j.NextRunAt, next), ct);
        if (mine == 0) return;
        var sales = await CountSalesAsync(db, now, zone, ct);
        db.DailySales.Add(sales);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // ближайшие 03:00 по времени магазина, в UTC
    private static DateTime NextThreeAm(
        DateTime utcNow, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo
            .ConvertTimeFromUtc(utcNow, zone);
        var next = local.Date.AddHours(3);
        if (next <= local)
            next = next.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(next, zone);
    }

    // продажи за вчера по времени магазина: оплаченные заказы и выручка
    private async Task<DailySale> CountSalesAsync(
        AppDb db, DateTime utcNow, TimeZoneInfo zone, CancellationToken ct)
    {
        var today = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone).Date;
        var from = TimeZoneInfo.ConvertTimeToUtc(today.AddDays(-1), zone);
        var to = TimeZoneInfo.ConvertTimeToUtc(today, zone);
        var paid = db.Orders.Where(o => o.Status == OrderStatus.Paid
                                     && o.CreatedAt >= from && o.CreatedAt < to);
        var orders = await paid.CountAsync(ct);
        var revenue = await paid.SelectMany(o => o.Items)
            .SumAsync(i => i.Price * i.Quantity, ct);
        var day = DateOnly.FromDateTime(today.AddDays(-1));
        log.LogInformation(
            "Отчёт за {Day:dd.MM.yyyy}: заказов {Orders}, выручка {Revenue:0.##} ₽",
            day, orders, revenue);
        return new DailySale(day, orders, revenue);
    }
}
