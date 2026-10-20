using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Data;

namespace Shop.Api;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDb _db;

    public OrdersController(AppDb db)
    {
        _db = db;
    }

    // Раздел 2: заказ и строка письма в таблице исходящих —
    // одним SaveChanges, одной транзакцией
    [HttpPost]
    public async Task<IActionResult> Create(
        NewOrder dto, CancellationToken ct)
    {
        var order = new Order(dto.UserId, dto.Items);
        _db.Orders.Add(order);
        // письмо — строкой в той же транзакции
        _db.PendingEmails.Add(
            new PendingEmail(order, dto.Email));
        await _db.SaveChangesAsync(ct);

        return Ok(order.Id);
    }

    // Раздел 11: оплата — одной командой с условием, как очистка.
    // Кто первым изменил строку, тот и прав; привычная оплата —
    // анти-пример Mistakes/LoadModifySavePay.cs
    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(
        Guid id, CancellationToken ct)
    {
        // сработает, только если заказ ещё новый
        var paid = await _db.Orders
            .Where(o => o.Id == id
                     && o.Status == OrderStatus.New)
            .ExecuteUpdateAsync(s => s.SetProperty(
                o => o.Status, OrderStatus.Paid), ct);
        if (paid == 1)
            return Ok();
        return await _db.Orders.AnyAsync(o => o.Id == id, ct)
            ? Conflict("Заказ уже оплачен или отменён")
            : NotFound();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? NotFound() : Ok(order);
    }
}
