using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Shop.Mistakes;

// Контроллер анти-примера называется так же, как настоящий (OrdersController),
// и держит только те действия, которые подменяет. Включён анти-пример —
// его действие встаёт на тот же маршрут, а настоящее снимается.
// Выключен — контроллера анти-примера в маршрутах нет.
public class MistakeRoutes(IConfiguration config) : IApplicationModelConvention
{
    public void Apply(ApplicationModel app)
    {
        var enabled = MistakeSwitch.Enabled(config).ToHashSet();
        foreach (var mistake in app.Controllers
                     .Where(c => MistakeSwitch.IsMistake(c.ControllerType))
                     .ToList())
        {
            if (!enabled.Contains(mistake.ControllerType.AsType()))
            {
                app.Controllers.Remove(mistake);
                continue;
            }
            var replaced = mistake.Actions.Select(Route).ToHashSet();
            foreach (var real in app.Controllers.Where(c =>
                         !MistakeSwitch.IsMistake(c.ControllerType)
                         && c.ControllerName == mistake.ControllerName))
                foreach (var action in real.Actions
                             .Where(a => replaced.Contains(Route(a))).ToList())
                    real.Actions.Remove(action);
        }
    }

    // «POST {id:guid}/pay» — метод и шаблон маршрута действия
    private static string Route(ActionModel action)
    {
        var http = action.Attributes.OfType<HttpMethodAttribute>().First();
        return string.Join(",", http.HttpMethods) + " " + http.Template;
    }
}
