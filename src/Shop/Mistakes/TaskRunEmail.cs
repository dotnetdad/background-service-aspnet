using Microsoft.AspNetCore.Mvc;
using Shop.Api;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.TaskRunEmail;

// Анти-пример раздела 1: письмо уходит в Task.Run, отметку ставит контекст запроса.
// Включается Mistakes__TaskRunEmail=true и подменяет POST /orders.
// Письмо приходит, отметки в заказе (emailSentAt) нет, в логе пусто:
// ObjectDisposedException лежит в таске, которую никто не ждёт (README, 01-task-run).
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
}
