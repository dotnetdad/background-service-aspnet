using Microsoft.EntityFrameworkCore;

namespace Shop.Data;

// Схема создаётся из модели на старте. У каждого тега своя схема:
// при переходе на другой тег — docker compose down -v (README).
public static class Schema
{
    public static void Create(IServiceProvider services)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                scope.ServiceProvider.GetRequiredService<AppDb>()
                    .Database.EnsureCreated();
                return;
            }
            // два инстанса на свежей базе создают схему одновременно:
            // проигравший подождёт и увидит, что таблицы уже есть
            catch (Exception) when (attempt < 5)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
    }
}
