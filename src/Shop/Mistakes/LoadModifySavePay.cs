using Microsoft.AspNetCore.Mvc;
using Shop.Data;

namespace Shop.Mistakes.LoadModifySavePay;

// Анти-пример раздела 11: оплата, написанная привычно, — как на тегах 01–10.
// Включается Mistakes__LoadModifySavePay=true и подменяет POST /orders/{id}/pay.
// Между загрузкой и сохранением очистка отменяет заказ, а SaveChanges пишет
// UPDATE по номеру заказа и старый статус не проверяет: отмена перезаписана,
// деньги взяты за отменённый заказ. Окно растягивает POST /chaos/pay/slow (README, 11-race).
[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDb _db;

    public OrdersController(AppDb db)
    {
        _db = db;
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
}
