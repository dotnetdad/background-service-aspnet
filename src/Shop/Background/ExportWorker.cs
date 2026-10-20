using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Shop.Chaos;
using Shop.Data;

namespace Shop.Background;

// Сервис выгрузок: раз в пять секунд забирает выгрузку — ждущую или ту, чья аренда
// вышла, — той же командой со SKIP LOCKED, что письма, и ставит в строку свою метку:
// новый Guid на каждую попытку. Пишет CSV частями по тысяче заказов в файл этой попытки
// в общем хранилище, после каждой части — прогресс и аренда ещё на минуту, одной
// командой с условием «ещё Running, и метка моя»: отменили или выгрузку забрал другой
// инстанс (часть писалась дольше аренды) — команда вернула ноль строк, и сервис бросает
// работу. Записанный целиком файл получает имя выгрузки, Done — тем же условием.
public class ExportWorker(
    IServiceScopeFactory scopes,
    ILogger<ExportWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(5));
        while (await timer
                   .WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunNextAsync(stoppingToken);
            }
            catch (Exception ex)
                when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(ex, "Выгрузка упала");
            }
        }
    }

    private async Task RunNextAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<AppDb>();
        // ждущую или ту, чья аренда вышла
        var now = DateTime.UtcNow;
        var lease = Guid.NewGuid(); // метка этой попытки
        var export = (await db.Exports
            .FromSql($"""
                UPDATE "Exports"
                SET "Status" = 'Running',
                    "LeaseToken" = {lease},
                    "LeaseUntil" = {now.AddMinutes(1)}
                WHERE "Id" = (
                    SELECT "Id" FROM "Exports"
                    WHERE "Status" = 'Pending'
                       OR ("Status" = 'Running'
                           AND "LeaseUntil" < {now})
                    ORDER BY "CreatedAt" LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .ToListAsync(ct)).SingleOrDefault();
        if (export is null) return;
        log.LogInformation("Выгрузка {Id} взята: заказов {Total}",
            export.Id, export.Total);

        // CSV — частями в файл этой попытки, не в память
        var path = ExportFiles.PartFor(export.Id, lease);
        await using var file = File.CreateText(path);
        var done = 0;
        await foreach (var part in OrderPagesAsync(
            db, export, 1000, ct))
        {
            await WriteCsvAsync(file, part);
            done += part.Count;
            // отменили или выгрузку забрали — ноль строк
            var mine = await db.Exports
                .Where(e => e.Id == export.Id
                         && e.Status == ExportStatus.Running
                         && e.LeaseToken == lease)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(e => e.Processed, done)
                    .SetProperty(e => e.LeaseUntil,
                        DateTime.UtcNow.AddMinutes(1)), ct);
            if (mine == 0) return;
        }

        // записан целиком — файл получает имя выгрузки
        await file.DisposeAsync();
        File.Move(path, ExportFiles.PathFor(export.Id), true);
        // Done — тоже с условием: отмену не затрём
        var finished = await db.Exports
            .Where(e => e.Id == export.Id
                     && e.Status == ExportStatus.Running
                     && e.LeaseToken == lease)
            .ExecuteUpdateAsync(s => s.SetProperty(
                e => e.Status, ExportStatus.Done), ct);
        if (finished == 0)
        {
            log.LogInformation(
                "Выгрузка {Id} не закончена: её отменили или забрал другой инстанс",
                export.Id);
            return;
        }
        log.LogInformation("Выгрузка {Id} готова", export.Id);
    }

    // заказы периода — частями, по порядку создания. Следующая часть начинается
    // сразу после последнего заказа прошлой (по индексу CreatedAt, Id), а не через
    // Skip: Skip заново проходит все пропущенные строки, и выгрузка росла бы квадратом.
    private static async IAsyncEnumerable<List<Order>> OrderPagesAsync(
        AppDb db, Export export, int size,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var afterAt = export.From;
        var afterId = Guid.Empty;
        while (true)
        {
            var part = await db.Orders.AsNoTracking()
                .Include(o => o.Items)
                .Where(o => o.CreatedAt >= export.From
                         && o.CreatedAt < export.To)
                .Where(o => EF.Functions.GreaterThan(
                    ValueTuple.Create(o.CreatedAt, o.Id),
                    ValueTuple.Create(afterAt, afterId)))
                .OrderBy(o => o.CreatedAt).ThenBy(o => o.Id)
                .Take(size)
                .ToListAsync(ct);
            if (part.Count == 0)
                yield break;
            await ExportChaos.PartAsync(ct); // /chaos/export/slow
            yield return part;
            if (part.Count < size)
                yield break;
            (afterAt, afterId) = (part[^1].CreatedAt, part[^1].Id);
        }
    }

    private static async Task WriteCsvAsync(
        StreamWriter file, List<Order> part)
    {
        if (file.BaseStream.Position == 0)
            await file.WriteLineAsync("id;created;status;items;total");
        foreach (var o in part)
            await file.WriteLineAsync(string.Create(
                CultureInfo.InvariantCulture,
                $"{o.Id};{o.CreatedAt:O};{o.Status};{o.Items.Count};{o.Items.Sum(i => i.Price * i.Quantity)}"));
        await file.FlushAsync();
    }
}
