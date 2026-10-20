namespace Shop.Chaos;

// База занята распродажей (/chaos/db/slow-views): запись пачки просмотров
// ждёт столько секунд, а просмотры тем временем копятся в канале.
public static class ViewsChaos
{
    public static int SaveSeconds { get; set; }

    public static Task SaveAsync(CancellationToken ct) =>
        SaveSeconds > 0
            ? Task.Delay(TimeSpan.FromSeconds(SaveSeconds), ct)
            : Task.CompletedTask;
}
