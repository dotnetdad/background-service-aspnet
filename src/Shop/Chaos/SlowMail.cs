using System.Net;
using System.Net.Sockets;

namespace Shop.Chaos;

// «Почта», которая отвечает медленно: сервер на свободном порту этого процесса.
// Приняв соединение, он ждёт MailChaos.SlowSeconds и только потом соединяет его
// с настоящей почтой. Ожидание идёт внутри отправки, поэтому таймаут отправки
// обрывает его так же, как обрывал бы медленную настоящую почту (/chaos/mail/slow).
public static class SlowMail
{
    private static readonly object Gate = new();
    private static TcpListener? _listener;

    // настоящая почта, к которой соединение уходит после ожидания
    public static (string Host, int Port) Target { get; set; }

    public static int Port
    {
        get
        {
            lock (Gate)
            {
                if (_listener is null)
                {
                    _listener = new TcpListener(IPAddress.Loopback, 0);
                    _listener.Start();
                    _ = AcceptAsync(_listener);
                }
                return ((IPEndPoint)_listener.LocalEndpoint).Port;
            }
        }
    }

    private static async Task AcceptAsync(TcpListener listener)
    {
        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            _ = RelayAsync(client, MailChaos.SlowSeconds, Target);
        }
    }

    private static async Task RelayAsync(
        TcpClient client, int seconds, (string Host, int Port) target)
    {
        using (client)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds));
                using var mail = new TcpClient();
                await mail.ConnectAsync(target.Host, target.Port);
                var fromClient = client.GetStream();
                var fromMail = mail.GetStream();
                // кто первым закрыл соединение — тот закрывает оба
                await Task.WhenAny(
                    fromClient.CopyToAsync(fromMail),
                    fromMail.CopyToAsync(fromClient));
            }
            catch (Exception)
            {
                // отправку оборвал таймаут, и клиент уже ушёл: закрываем и всё
            }
        }
    }
}
