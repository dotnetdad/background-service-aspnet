namespace Shop.Chaos;

// Медленная выгрузка из README (/chaos/export/slow): каждая часть в тысячу заказов
// пишется столько секунд — как на большой базе, чтобы успеть увидеть прогресс и отменить.
public static class ExportChaos
{
    public static int PartSeconds { get; set; }

    public static Task PartAsync(CancellationToken ct) =>
        PartSeconds > 0
            ? Task.Delay(TimeSpan.FromSeconds(PartSeconds), ct)
            : Task.CompletedTask;
}
