using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Background;
using Shop.Data;

namespace Shop.Api;

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
        // не ждём: полон — вытеснится самый старый
        _views.Writer.TryWrite(new ProductView(id));
        return Ok(product);
    }

    // «Популярное»: товары с самым большим числом просмотров за последний час
    [HttpGet("popular")]
    public async Task<IActionResult> Popular(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddHours(-1);
        var top = await _db.ProductViews
            .Where(v => v.ViewedAt >= since)
            .GroupBy(v => v.ProductId)
            .Select(g => new { ProductId = g.Key, Views = g.Count() })
            .OrderByDescending(x => x.Views)
            .Take(10)
            .ToListAsync(ct);
        return Ok(top);
    }
}
