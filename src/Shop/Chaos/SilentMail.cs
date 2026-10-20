using System.Net;
using System.Net.Sockets;

namespace Shop.Chaos;

// «Почта», которая принимает соединение и молчит: сервер на свободном порту
// этого процесса. Соединения он держит открытыми и не отвечает ни байтом,
// поэтому отправка ждёт, пока её не оборвёт таймаут (/chaos/mail/silent).
public static class SilentMail
{
    private static readonly object Gate = new();
    private static readonly List<TcpClient> Held = [];
    private static TcpListener? _listener;

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
            lock (Gate)
            {
                Held.RemoveAll(c => !c.Connected);
                Held.Add(client);
            }
        }
    }
}
