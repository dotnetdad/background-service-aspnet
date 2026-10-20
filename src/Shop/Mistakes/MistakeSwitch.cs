namespace Shop.Mistakes;

// Анти-примеры гайда. Каждый лежит в своём файле Mistakes/<Имя>.cs,
// в пространстве имён Shop.Mistakes.<Имя>, и включается настройкой
// Mistakes__<Имя>=true (docker-compose.override.yml, README, «Анти-примеры»).
//
// Класс анти-примера называется так же, как настоящий, и подменяет его:
// фоновый сервис, почту или канал — в контейнере зависимостей (здесь),
// действие контроллера — в маршрутах (MistakeRoutes).
public static class MistakeSwitch
{
    private const string Prefix = "Shop.Mistakes.";

    public static IEnumerable<Type> Enabled(IConfiguration config) =>
        typeof(MistakeSwitch).Assembly.GetTypes().Where(t =>
            t is { IsClass: true, IsAbstract: false, IsNested: false }
            && t.Namespace is { } ns && ns.StartsWith(Prefix)
            && config.GetValue<bool>("Mistakes:" + ns[Prefix.Length..]));

    public static bool IsMistake(Type t) =>
        t.Namespace?.StartsWith(Prefix) == true;

    public static IServiceCollection AddMistakes(
        this IServiceCollection services, IConfiguration config)
    {
        foreach (var mistake in Enabled(config))
        {
            if (typeof(Microsoft.AspNetCore.Mvc.ControllerBase)
                .IsAssignableFrom(mistake))
                continue; // контроллеры подменяет MistakeRoutes

            var real = services.Where(d =>
                d.ImplementationType is { } impl
                && impl.Name == mistake.Name && !IsMistake(impl)).ToList();
            foreach (var d in real)
            {
                // AddSingleton<ProductViews>() — тип сервиса и есть реализация:
                // настоящий остаётся для того, что анти-пример не подменяет,
                // а рядом встаёт анти-пример, его берут подменённые классы
                if (d.ServiceType == d.ImplementationType)
                {
                    services.Add(new ServiceDescriptor(
                        mistake, mistake, d.Lifetime));
                    continue;
                }
                services.Remove(d);
                services.Add(new ServiceDescriptor(
                    d.ServiceType, mistake, d.Lifetime));
            }
            if (real.Count == 0
                && typeof(IHostedService).IsAssignableFrom(mistake))
                services.AddSingleton(typeof(IHostedService), mistake);
        }
        return services;
    }
}
