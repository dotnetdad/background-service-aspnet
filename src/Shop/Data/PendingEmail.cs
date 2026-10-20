namespace Shop.Data;

// Строка таблицы исходящих писем
public class PendingEmail
{
    private PendingEmail() { }

    public PendingEmail(Order order, string to)
    {
        Order = order;
        To = to;
    }

    public Guid Id { get; private set; }
        = Guid.CreateVersion7();
    public Guid OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public string To { get; private set; } = "";

    // пять ошибок — письмо ждёт разбора
    public const int MaxAttempts = 5;

    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; private set; }
        = DateTime.UtcNow;
    // сколько раз почта ответила ошибкой
    public int Attempts { get; set; }
    // раньше этого времени письмо не берём
    public DateTime NextTryAt { get; set; }
        = DateTime.UtcNow;
}
