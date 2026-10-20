namespace Shop.Data;

// Отчёт о продажах за день: оплаченные заказы и выручка
public class DailySale
{
    private DailySale() { }

    public DailySale(DateOnly day, int orders, decimal revenue)
    {
        Day = day;
        Orders = orders;
        Revenue = revenue;
    }

    public int Id { get; private set; }
    // день по времени магазина
    public DateOnly Day { get; private set; }
    public int Orders { get; private set; }
    public decimal Revenue { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
}
