using System.Net.Mail;
using Microsoft.Extensions.Options;
using Shop.Chaos;
using Shop.Data;
using Shop.Mail;

namespace Shop.Mistakes.SharedSmtpClient;

// Анти-пример раздела 9: один SmtpClient на всё приложение, по привычке к HttpClient.
// Включается Mistakes__SharedSmtpClient=true и подменяет почту магазина.
// SmtpClient шлёт одно письмо за раз: пока он занят письмом одного цикла рассылки,
// письмо соседнего получает InvalidOperationException: An asynchronous call is already
// in progress — и у него сгорает попытка, хотя почта жива (README, 09-loops).
public class SmtpOrderMail(IOptions<MailSettings> settings) : IOrderMail
{
    // Анти-пример: один клиент на всё приложение
    private readonly SmtpClient _smtp =
        new(settings.Value.Host, settings.Value.Port);

    private async Task SendMessageAsync(MailMessage message)
    {
        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));
        await _smtp.SendMailAsync(message, timeout.Token);
    }

    public async Task SendAsync(string to, Order order)
    {
        await MailChaos.BeforeSendAsync(to);
        using var message = new MailMessage(
            "shop@example.com", to,
            $"Заказ {order.Id} принят",
            "Состав заказа:\n" + string.Join("\n", order.Items.Select(i =>
                $"{i.Product} × {i.Quantity} — {i.Price * i.Quantity} ₽")));
        await SendMessageAsync(message);
        MailChaos.AfterSend();
    }
}
