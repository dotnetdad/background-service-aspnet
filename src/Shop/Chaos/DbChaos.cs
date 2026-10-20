using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shop.Data;

namespace Shop.Chaos;

// Сбои базы из README: включаются запросами /chaos/... (Api/ChaosController.cs).
// Перехватчики EF Core подключает AppDb.OnConfiguring.
public static class DbChaos
{
    private static int _crashAfterSave;

    public static readonly IInterceptor[] Interceptors = [new Saves()];

    // SaveChanges, который ставит заказу Paid, ждёт столько секунд перед записью:
    // окно между загрузкой и сохранением у привычной оплаты (/chaos/pay/slow)
    public static int PaySlowSeconds { get; set; }

    // то же для Cancelled — у очистки загрузкой (/chaos/cleanup/slow)
    public static int CleanupSlowSeconds { get; set; }

    public static void CrashAfterNextSave() =>
        Interlocked.Exchange(ref _crashAfterSave, 1);

    public static void Reset()
    {
        PaySlowSeconds = 0;
        CleanupSlowSeconds = 0;
    }

    private static void CrashIfAsked()
    {
        if (Interlocked.Exchange(ref _crashAfterSave, 0) == 1)
            Environment.FailFast(
                "Процесс упал сразу после сохранения (сбой: /chaos/crash-after-save)");
    }

    private static int SlowSeconds(DbContext? db) =>
        db?.ChangeTracker.Entries<Order>()
            .Where(e => e.State == EntityState.Modified
                     && e.Property(o => o.Status).IsModified)
            .Select(e => e.Entity.Status switch
            {
                OrderStatus.Paid => PaySlowSeconds,
                OrderStatus.Cancelled => CleanupSlowSeconds,
                _ => 0
            })
            .DefaultIfEmpty(0).Max() ?? 0;

    private class Saves : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData e, InterceptionResult<int> result,
            CancellationToken ct = default)
        {
            var seconds = SlowSeconds(e.Context);
            if (seconds > 0)
            {
                // в лог, чтобы видеть, когда окно открылось
                Console.WriteLine(
                    $"Сбой /chaos: смена статуса заказа ждёт {seconds} с перед сохранением");
                await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
            }
            return result;
        }

        public override int SavedChanges(
            SaveChangesCompletedEventData e, int result)
        {
            CrashIfAsked();
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData e, int result,
            CancellationToken ct = default)
        {
            CrashIfAsked();
            return ValueTask.FromResult(result);
        }
    }
}
