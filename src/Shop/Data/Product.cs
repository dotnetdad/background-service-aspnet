namespace Shop.Data;

// Товар магазина. Товары создаются вместе с базой (AppDb, HasData): 1–100, 77 — кружка.
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}

// Просмотр страницы товара: и элемент канала, и строка таблицы ProductViews
public class ProductView
{
    private ProductView() { }

    public ProductView(int productId)
    {
        ProductId = productId;
    }

    public long Id { get; private set; }
    public int ProductId { get; private set; }
    public DateTime ViewedAt { get; private set; } = DateTime.UtcNow;
}
