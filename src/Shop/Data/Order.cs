using Shop.Api;

namespace Shop.Data;

public enum OrderStatus { New, Paid, Cancelled }

public class Order
{
    private Order() { }

    public Order(Guid userId, IEnumerable<NewOrderItem> items)
    {
        UserId = userId;
        Items = items
            .Select(i => new OrderItem(i.Product, i.Quantity, i.Price))
            .ToList();
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; set; } = OrderStatus.New;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    // Отметка об отправке в самом заказе — только для раздела 1 (Task.Run).
    // С раздела 2 она живёт в строке письма (PendingEmail.SentAt).
    public DateTime? EmailSentAt { get; set; }
    public List<OrderItem> Items { get; private set; } = [];
}

public class OrderItem
{
    private OrderItem() { }

    public OrderItem(string product, int quantity, decimal price)
    {
        Product = product;
        Quantity = quantity;
        Price = price;
    }

    public int Id { get; private set; }
    public string Product { get; private set; } = "";
    public int Quantity { get; private set; }
    public decimal Price { get; private set; }
}
