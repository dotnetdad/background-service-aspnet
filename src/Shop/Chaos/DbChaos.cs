using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Shop.Chaos;

// Сбои базы из README: включаются запросами /chaos/... (Api/ChaosController.cs).
// Перехватчики EF Core подключает AppDb.OnConfiguring.
public static class DbChaos
{
    private static int _crashAfterSave;

    public static readonly IInterceptor[] Interceptors = [new Saves()];

    public static void CrashAfterNextSave() =>
        Interlocked.Exchange(ref _crashAfterSave, 1);

    private static void CrashIfAsked()
    {
        if (Interlocked.Exchange(ref _crashAfterSave, 0) == 1)
            Environment.FailFast(
                "Процесс упал сразу после сохранения (сбой: /chaos/crash-after-save)");
    }

    private class Saves : SaveChangesInterceptor
    {
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
