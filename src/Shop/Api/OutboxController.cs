using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Data;

namespace Shop.Api;

// Служебный адрес для дежурного: письма после пяти ошибок ждут человека.
// Почта поднялась — POST /outbox/given-up/retry возвращает их в очередь.
[ApiController]
[Route("outbox")]
public class OutboxController : ControllerBase
{
    private readonly AppDb _db;

    public OutboxController(AppDb db)
    {
        _db = db;
    }

    // разбор после аварии: сдавшиеся — снова в очередь
    [HttpPost("given-up/retry")]
    public async Task<IActionResult> RetryGivenUp(
        CancellationToken ct)
    {
        var back = await _db.PendingEmails
            .Where(p => p.SentAt == null
                     && p.Attempts >= PendingEmail.MaxAttempts)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Attempts, 0)
                .SetProperty(p => p.NextTryAt, DateTime.UtcNow),
                ct);
        return Ok(back);
    }
}
