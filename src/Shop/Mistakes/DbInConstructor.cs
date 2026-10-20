using Microsoft.EntityFrameworkCore;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.DbInConstructor;

// Анти-пример раздела 3. Включается Mistakes__DbInConstructor=true и подменяет рассылку.
// В дев-режиме (так магазин запущен в compose) приложение не стартует:
// Cannot consume scoped service 'Shop.Data.AppDb' from singleton.
// В проде (ASPNETCORE_ENVIRONMENT=Production) стартует, и один контекст живёт
// столько же, сколько процесс: проход за проходом он держит всё, что загрузил (README, 03-worker).
// Анти-пример: контекст базы в конструкторе
public class EmailOutboxWorker(
    AppDb db, IOrderMail mail) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(5));
        while (await timer
                   .WaitForNextTickAsync(stoppingToken))
        {
            var batch = await db.PendingEmails
                .Include(p => p.Order)
                .ThenInclude(o => o.Items)
                .Where(p => p.SentAt == null)
                .OrderBy(p => p.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(stoppingToken);
            foreach (var p in batch)
            {
                await mail.SendAsync(p.To, p.Order);
                p.SentAt = DateTime.UtcNow;
                await db.SaveChangesAsync(stoppingToken);
            }
            // логгера в конструкторе нет — пишем прямо в консоль
            Console.WriteLine(
                $"Контекст рассылки держит объектов: {db.ChangeTracker.Entries().Count()}");
        }
    }

    // всё, кроме контекста, — как у настоящей рассылки (Background/EmailOutboxWorker.cs)
    private const int BatchSize = 20;
}
