using System.Threading.Channels;
using Shop.Data;

namespace Shop.Background;

// Очередь в памяти между страницей товара и фоном. Ограничена тысячей просмотров:
// полна — новый вытесняет самый старый. Просмотр можно потерять, поэтому не таблица.
// Просмотры: часть можно потерять
public class ProductViews
{
    private readonly Channel<ProductView> _channel =
        Channel.CreateBounded<ProductView>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    // страница только пишет, сервис только читает
    public ChannelWriter<ProductView> Writer =>
        _channel.Writer;
    public ChannelReader<ProductView> Reader =>
        _channel.Reader;
}
