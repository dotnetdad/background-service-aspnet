using System.Net.Mail;

namespace Shop.Chaos;

// Сбои почты из README: включаются запросами /chaos/mail/... (Api/ChaosController.cs).
// Свои у каждого процесса: у второго инстанса — свой адрес /chaos.
public static class MailChaos
{
    private static int _failNext;

    // почта отвечает медленно: столько секунд на каждое письмо
    public static int SlowSeconds { get; set; }

    public static void FailNext() => Interlocked.Increment(ref _failNext);

    public static void Reset()
    {
        Interlocked.Exchange(ref _failNext, 0);
        SlowSeconds = 0;
    }

    // Зовёт почта магазина перед каждой отправкой.
    public static async Task BeforeSendAsync(string to)
    {
        if (SlowSeconds > 0)
            await Task.Delay(TimeSpan.FromSeconds(SlowSeconds));
        if (Take(ref _failNext))
            throw new SmtpException(
                "Почта ответила ошибкой (сбой: /chaos/mail/fail-next)");
    }

    private static bool Take(ref int counter)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref counter);
            if (seen <= 0)
                return false;
        }
        while (Interlocked.CompareExchange(ref counter, seen - 1, seen) != seen);
        return true;
    }
}
