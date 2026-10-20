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
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; private set; }
        = DateTime.UtcNow;
}
