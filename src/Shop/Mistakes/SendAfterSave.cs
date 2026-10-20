using Microsoft.AspNetCore.Mvc;
using Shop.Api;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.SendAfterSave;

// Анти-пример раздела 2: письмо прямо в запросе, сразу после сохранения заказа.
// Включается Mistakes__SendAfterSave=true и подменяет POST /orders.
// Попробуй POST /chaos/crash-after-save и заказ: процесс падает между сохранением
// и отправкой — заказ в базе есть, письма нет, и вспомнить о нём некому (README, 02-outbox).
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

    // Анти-пример: письмо сразу после сохранения
    [HttpPost]
    public async Task<IActionResult> Create(
        NewOrder dto, CancellationToken ct)
    {
        var order = new Order(dto.UserId, dto.Items);
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
        // упал здесь — заказ есть, письма нет
        await _mail.SendAsync(dto.Email, order);
        return Ok(order.Id);
    }
}
