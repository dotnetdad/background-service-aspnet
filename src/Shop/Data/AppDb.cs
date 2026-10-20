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
}
