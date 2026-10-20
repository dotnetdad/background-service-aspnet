using System.Net.Mail;
using Microsoft.Extensions.Options;
using Shop.Chaos;
using Shop.Data;

namespace Shop.Mail;

// Почта магазина: письмо о заказе с его составом.
// Почтовый клиент — свой на каждое письмо: SmtpClient шлёт одно письмо за раз.
public class SmtpOrderMail(IOptions<MailSettings> settings) : IOrderMail
{
    public async Task SendAsync(string to, Order order)
    {
        // сбои из README: /chaos/mail/...
        await MailChaos.BeforeSendAsync(to);
        using var message = new MailMessage(
            "shop@example.com", to,
            $"Заказ {order.Id} принят",
            Body(order));
        await SendMessageAsync(message);
        // сбой из README: /chaos/crash-after-send
        MailChaos.AfterSend();
    }

    private async Task SendMessageAsync(MailMessage message)
    {
        using var smtp = new SmtpClient(
            settings.Value.Host, settings.Value.Port);
        // почта молчит дольше десяти секунд — ошибка
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        await smtp.SendMailAsync(message, timeout.Token);
    }

    private static string Body(Order order) =>
        "Состав заказа:\n" + string.Join("\n", order.Items.Select(i =>
            $"{i.Product} × {i.Quantity} — {i.Price * i.Quantity} ₽"));
}
