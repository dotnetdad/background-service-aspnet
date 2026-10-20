using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Background;
using Shop.Data;

namespace Shop.Api;

// Выгрузка заказов для менеджера: запрос пишет строку и сразу отвечает 202,
// работу делает фоновый сервис (Background/ExportWorker.cs).
[ApiController]
[Route("exports")]
public class ExportsController : ControllerBase
{
    private readonly AppDb _db;

    public ExportsController(AppDb db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Start(
        NewExport dto, CancellationToken ct)
    {
        // сколько всего — один count, это быстро
        var total = await _db.Orders.CountAsync(
            o => o.CreatedAt >= dto.From
              && o.CreatedAt < dto.To, ct);
        var export = new Export(dto.From, dto.To, total);
        _db.Exports.Add(export);
        await _db.SaveChangesAsync(ct);
        // 202: принято, статус — по адресу из Location
        return AcceptedAtAction(nameof(Get),
            new { id = export.Id },
            new { export.Id, export.Total });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(
        Guid id, CancellationToken ct) =>
        await _db.Exports.FindAsync([id], ct) is { } e
            ? Ok(new
            {
                Status = e.Status.ToString(),
                e.Processed,
                e.Total,
                // готово — адрес файла
                File = e.Status == ExportStatus.Done
                    ? Url.Action(nameof(Download), new { id })
                    : null
            })
            : NotFound();

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Download(
        Guid id, CancellationToken ct) =>
        await _db.Exports.FindAsync([id], ct) is
            { Status: ExportStatus.Done }
            ? PhysicalFile(ExportFiles.PathFor(id),
                "text/csv", "orders.csv")
            : NotFound();

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id, CancellationToken ct)
    {
        var n = await _db.Exports
            .Where(e => e.Id == id
                     && (e.Status == ExportStatus.Pending
                      || e.Status == ExportStatus.Running))
            .ExecuteUpdateAsync(s => s.SetProperty(
                e => e.Status, ExportStatus.Cancelled), ct);
        if (n == 1)
            return Accepted();
        return await _db.Exports.AnyAsync(e => e.Id == id, ct)
            ? Conflict("Выгрузка уже готова или отменена")
            : NotFound();
    }
}
