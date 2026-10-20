using Microsoft.EntityFrameworkCore;
using Shop.Chaos;

namespace Shop.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PendingEmail> PendingEmails => Set<PendingEmail>();
    public DbSet<ScheduledJob> ScheduledJobs => Set<ScheduledJob>();
    public DbSet<DailySale> DailySales => Set<DailySale>();
    public DbSet<Export> Exports => Set<Export>();

    // сбои базы из README (/chaos/...)
    protected override void OnConfiguring(DbContextOptionsBuilder o) =>
        o.AddInterceptors(DbChaos.Interceptors);

    protected override void OnModelCreating(ModelBuilder b)
    {
        // индекс только по письмам, которые ждут
        b.Entity<PendingEmail>()
            .HasIndex(p => p.NextTryAt)
            .HasFilter("\"SentAt\" IS NULL AND \"Attempts\" < "
                       + PendingEmail.MaxAttempts);

        // очистка ищет новые заказы по времени создания
        b.Entity<Order>()
            .HasIndex(o => new { o.Status, o.CreatedAt });

        // строка отчёта — одна, создаётся вместе с базой;
        // время в прошлом: первый тик посчитает отчёт
        b.Entity<ScheduledJob>().HasData(new ScheduledJob
        {
            Name = "daily-sales",
            NextRunAt = DateTime.UnixEpoch
        });

        // статус выгрузки — строкой: его сравнивает SQL
        b.Entity<Export>()
            .Property(e => e.Status)
            .HasConversion<string>();
        // выгрузка и её count ищут заказы по времени создания,
        // а части идут по (CreatedAt, Id) — после последнего заказа прошлой
        b.Entity<Order>()
            .HasIndex(o => new { o.CreatedAt, o.Id });
    }
}
