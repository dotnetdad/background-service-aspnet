using Microsoft.EntityFrameworkCore;
using Shop.Chaos;

namespace Shop.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PendingEmail> PendingEmails => Set<PendingEmail>();

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
    }
}
