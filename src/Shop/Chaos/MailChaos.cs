using System.Net.Mail;
using Shop.Mail;

namespace Shop.Chaos;

// Сбои почты из README: включаются запросами /chaos/mail/... (Api/ChaosController.cs).
// Свои у каждого процесса: у второго инстанса — свой адрес /chaos.
public static class MailChaos
{
    private static int _failNext;
    private static (string Host, int Port)? _realMail;

    // почта лежит: каждое письмо — SmtpException
    public static bool Down { get; set; }

    // почта отвечает медленно: столько секунд на каждое письмо (SlowMail)
    public static int SlowSeconds { get; private set; }

    public static void FailNext() => Interlocked.Increment(ref _failNext);

    // Почта принимает соединение и молчит: настройки почты магазина
    // показывают на сервер этого процесса, который ничего не отвечает (SilentMail).
    public static void Silence(MailSettings mail)
    {
        _realMail ??= (mail.Host, mail.Port);
        mail.Host = "127.0.0.1";
        mail.Port = SilentMail.Port;
    }

    // Почта отвечает медленно: настройки почты магазина показывают на сервер
    // этого процесса, который ждёт столько секунд и соединяет с настоящей (SlowMail).
    // Ожидание — внутри отправки, и таймаут отправки его обрывает.
    public static void Slow(MailSettings mail, int seconds)
    {
        _realMail ??= (mail.Host, mail.Port);
        SlowSeconds = seconds;
        SlowMail.Target = _realMail.Value;
        mail.Host = "127.0.0.1";
        mail.Port = SlowMail.Port;
    }

    public static void Reset(MailSettings mail)
    {
        Interlocked.Exchange(ref _failNext, 0);
        Down = false;
        SlowSeconds = 0;
        if (_realMail is { } real)
            (mail.Host, mail.Port) = real;
        _realMail = null;
    }

    // Зовёт почта магазина перед каждой отправкой.
    public static Task BeforeSendAsync(string to)
    {
        if (Down)
            throw new SmtpException(
                "Почта лежит (сбой: /chaos/mail/down)");
        if (Take(ref _failNext))
            throw new SmtpException(
                "Почта ответила ошибкой (сбой: /chaos/mail/fail-next)");
        // .invalid — зарезервированный домен: такого ящика нет и не будет
        if (to.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
            throw new SmtpFailedRecipientException(
                SmtpStatusCode.MailboxUnavailable, to,
                "Ящика не существует (адрес на .invalid)");
        return Task.CompletedTask;
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
