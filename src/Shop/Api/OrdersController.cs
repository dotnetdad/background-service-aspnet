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

    // загрузить, проверить, поменять, сохранить
    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(
        Guid id, CancellationToken ct)
    {
        var order = await _db.Orders.FindAsync([id], ct);
        if (order is null)
            return NotFound();
        if (order.Status != OrderStatus.New)
            return Conflict("Заказ уже оплачен или отменён");
        order.Status = OrderStatus.Paid;
        await _db.SaveChangesAsync(ct);
        return Ok();
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
