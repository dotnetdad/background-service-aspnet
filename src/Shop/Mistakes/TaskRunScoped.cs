using Microsoft.AspNetCore.Mvc;
using Shop.Api;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.TaskRunScoped;

// Анти-пример раздела 1: Task.Run, починенный на месте, — свой scope и try в лямбде.
// Включается Mistakes__TaskRunScoped=true и подменяет POST /orders.
// Отметка ставится, ошибка почты попадает в лог. А деплой письмо всё равно обрывает:
// хост о лямбде не знает, процесс при выходе её не ждёт (README, 01-task-run).
[ApiController]
[Route("orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDb _db;
    private readonly IOrderMail _mail;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OrdersController> _log;

    public OrdersController(
        AppDb db, IOrderMail mail,
        IServiceScopeFactory scopes, ILogger<OrdersController> log)
    {
        _db = db;
        _mail = mail;
        _scopes = scopes;
        _log = log;
    }

    [HttpPost]
    public async Task<IActionResult> Create(NewOrder dto)
    {
        var order = new Order(dto.UserId, dto.Items);
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // свой scope и try внутри лямбды
        _ = Task.Run(async () =>
        {
            try
            {
                await _mail.SendAsync(dto.Email, order);
                await using var scope = _scopes.CreateAsyncScope();
                var db = scope.ServiceProvider
                    .GetRequiredService<AppDb>();
                var saved = await db.Orders.FindAsync(order.Id);
                saved!.EmailSentAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Письмо к заказу {Id} не ушло",
                    order.Id);
            }
        });

        return Ok(order.Id);
    }
}
