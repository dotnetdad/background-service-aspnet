namespace Shop.Api;

public record NewOrder(Guid UserId, string Email, List<NewOrderItem> Items);

public record NewOrderItem(string Product, int Quantity, decimal Price);
