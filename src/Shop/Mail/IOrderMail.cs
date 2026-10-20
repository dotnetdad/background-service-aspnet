using Shop.Data;

namespace Shop.Mail;

// Почта магазина, singleton
public interface IOrderMail
{
    // письмо о заказе: текст собирается из заказа
    Task SendAsync(string to, Order order);
}
