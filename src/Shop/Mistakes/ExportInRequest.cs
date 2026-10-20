using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Api;
using Shop.Chaos;
using Shop.Data;

namespace Shop.Mistakes.ExportInRequest;

// Анти-пример раздела 13: выгрузка прямо в запросе.
// Включается Mistakes__ExportInRequest=true и подменяет POST /exports.
// Запрос на две минуты: прокси перед магазином (в compose — proxy, порт 8088) ждёт
// ответа минуту и отдаёт 504, а запрос на сервере работает дальше — токена в выгрузке
// нет. Менеджер жмёт ещё раз — и две выгрузки грузят базу одновременно (README, 13-export).
[ApiController]
[Route("exports")]
public class ExportsController : ControllerBase
{
    private readonly AppDb _db;
    private readonly ILogger<ExportsController> _log;

    public ExportsController(AppDb db, ILogger<ExportsController> log)
    {
        _db = db;
        _log = log;
    }

    // Анти-пример: выгрузка прямо в запросе
    [HttpPost]
    public async Task<IActionResult> Export(NewExport dto)
    {
        var csv = await BuildCsvAsync(dto.From, dto.To);
        return File(Encoding.UTF8.GetBytes(csv),
            "text/csv", "orders.csv");
    }

    // вся выгрузка — в памяти и без токена: клиент ушёл, а работа идёт
    private async Task<string> BuildCsvAsync(DateTime from, DateTime to)
    {
        var orders = _db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= from && o.CreatedAt < to);
        var total = await orders.CountAsync();
        var csv = new StringBuilder("id;created;status;items;total\n");
        // части — как у сервиса выгрузок: после последнего заказа прошлой части
        var (afterAt, afterId) = (from, Guid.Empty);
        for (var done = 0; done < total; )
        {
            var part = await orders.Include(o => o.Items)
                .Where(o => EF.Functions.GreaterThan(
                    ValueTuple.Create(o.CreatedAt, o.Id),
                    ValueTuple.Create(afterAt, afterId)))
                .OrderBy(o => o.CreatedAt).ThenBy(o => o.Id)
                .Take(1000)
                .ToListAsync();
            if (part.Count == 0)
                break;
            (afterAt, afterId) = (part[^1].CreatedAt, part[^1].Id);
            await ExportChaos.PartAsync(CancellationToken.None);
            foreach (var o in part)
                csv.Append(CultureInfo.InvariantCulture,
                    $"{o.Id};{o.CreatedAt:O};{o.Status};{o.Items.Count};{o.Items.Sum(i => i.Price * i.Quantity)}\n");
            done += part.Count;
            _log.LogInformation("Выгрузка в запросе: {Done} из {Total}",
                done, total);
        }
        return csv.ToString();
    }
}
