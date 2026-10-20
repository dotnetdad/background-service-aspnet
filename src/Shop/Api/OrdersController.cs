using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Api;

[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDb _db;
    private readonly IOrderMail _mail;

    public OrdersController(AppDb db, IOrderMail mail)
    {
        _db = db;
        _mail = mail;
    }

    // Раздел 1: письмо уходит в Task.Run, отметку ставит контекст запроса.
    // Письмо приходит, отметки нет, в логе пусто (README, 01-task-run).
    [HttpPost]
    public async Task<IActionResult> Create(NewOrder dto)
    {
        var order = new Order(dto.UserId, dto.Items);
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        _ = Task.Run(async () =>
        {
            await _mail.SendAsync(dto.Email, order);
            order.EmailSentAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        });

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
