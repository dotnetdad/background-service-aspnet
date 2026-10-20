using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Shop.Chaos;
using Shop.Data;

namespace Shop.Mistakes.UnboundedViews;

// Анти-пример раздела 14: канал просмотров без лимита.
// Включается Mistakes__UnboundedViews=true и подменяет канал, сервис просмотров
// и страницу товара. База притормозила (POST /chaos/db/slow-views), а просмотры
// идут потоком (POST /chaos/views/flood): канал растёт без края, память — вместе
// с ним, пока процесс не упрётся в лимит памяти контейнера и не упадёт
// вместе с заказами и рассылкой (README, 14-channel).
public class ProductViews
{
    // Анти-пример: канал без лимита
    private readonly Channel<ProductView> _channel =
        Channel.CreateUnbounded<ProductView>();

    public ChannelWriter<ProductView> Writer =>
        _channel.Writer;
    public ChannelReader<ProductView> Reader =>
        _channel.Reader;
}

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
                await ViewsChaos.SaveAsync(stoppingToken);
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider
                    .GetRequiredService<AppDb>();
                db.ProductViews.AddRange(batch);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
                when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Просмотры не записаны");
            }
        }
    }
}

[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    private readonly AppDb _db;
    private readonly ProductViews _views;

    public ProductsController(AppDb db, ProductViews views)
    {
        _db = db;
        _views = views;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(
        int id, CancellationToken ct)
    {
        var product = await _db.Products
            .FindAsync([id], ct);
        if (product is null)
            return NotFound();
        _views.Writer.TryWrite(new ProductView(id));
        return Ok(product);
    }
}
