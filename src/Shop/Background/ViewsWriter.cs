using Shop.Chaos;
using Shop.Data;

namespace Shop.Background;

// Сервис просмотров: ждёт, пока в канале что-то появится, забирает всё, что лежит,
// но не больше ста, и пишет в базу одним SaveChanges. Упала запись — пачка потеряна,
// строка в лог, цикл живёт: это та же допустимая потеря.
public class ViewsWriter(
    ProductViews views,
    IServiceScopeFactory scopes,
    ILogger<ViewsWriter> log) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        var reader = views.Reader;
        while (await reader.WaitToReadAsync(stoppingToken))
        {
            var batch = new List<ProductView>();
            while (batch.Count < 100
                   && reader.TryRead(out var view))
                batch.Add(view);
            try
            {
                await SaveAsync(batch, stoppingToken);
            }
            catch (Exception ex)
                when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Просмотры не записаны");
            }
        }
    }

    private async Task SaveAsync(
        List<ProductView> batch, CancellationToken ct)
    {
        await ViewsChaos.SaveAsync(ct); // /chaos/db/slow-views
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AppDb>();
        db.ProductViews.AddRange(batch);
        await db.SaveChangesAsync(ct);
    }
}
