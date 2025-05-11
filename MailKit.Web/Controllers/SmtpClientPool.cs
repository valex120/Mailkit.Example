using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Collections.Concurrent;
using System.Timers;
using Timer = System.Timers.Timer;

namespace MailKit.Web.Controllers
{
    /// <summary>
    ///     Пул подключений к SMTP серверу
    /// </summary>
    public sealed class SmtpClientPool : IDisposable
    {
        private readonly AutoResetEvent _wait = new AutoResetEvent(false);
        private readonly Timer _timer = new Timer(TimeSpan.FromSeconds(10));

        private readonly ConcurrentBag<SmtpClient> _clients = new ConcurrentBag<SmtpClient>();

        public SmtpClientPool(int connectionsCount = 1)
        {
            _clients = new ConcurrentBag<SmtpClient>();
            for (int i = 0; i < connectionsCount; i++)
                _clients.Add(new SmtpClient());

            _wait.Set();           
            _timer.Elapsed += SendNoOp;
            _timer.AutoReset = true;
            _timer.Start();
        }

        /// <summary>
        ///     Отправляет письмо
        /// </summary>
        /// <param name="message">Сообщение с письмом</param>
        /// <param name="token">Токен отмены операции</param>
        public async Task SendAsync(MimeMessage message, CancellationToken token)
        {
            SmtpClient? client;
            while (_clients.TryTake(out client) is false)
                _wait.WaitOne();

            try
            {
                if (client.IsConnected is false)
                    await client.ConnectAsync(token);

                await client.SendAsync(message, token);
            }
            finally
            {
                _clients.Add(client);
                _wait.Set();
            }
        }

        /// <summary>
        ///     Отправляет команду NoOp для поддержания соединения открытым
        /// </summary>
        private async void SendNoOp(object? sender, ElapsedEventArgs e)
        {
            var returnClients = new List<SmtpClient>();

            if (_clients.TryPeek(out _) is false)
                return;

            try
            {
                while (_clients.TryTake(out var client))
                    returnClients.Add(client);

                await Task.WhenAll(returnClients.Select(async c => 
                {
                    if (c.IsConnected)
                        await c.NoOpAsync();
                }));
            }
            finally
            {
                foreach (var returnClient in returnClients)
                    _clients.Add(returnClient);

                _wait.Set();
            }
        }

        public void Dispose()
        {
            _timer.Elapsed -= SendNoOp;
            _timer.Dispose();

            foreach (var client in _clients)
                client.Dispose();

            _wait.Dispose();
        }
    }
}

public static class SmtpClientExtensions
{
    private const string _server = "smtp.gmail.com";
    private const string _login = "youremailhere@gmail.com";
    private const string _password = "your app password here";
    private const int _port = 587;

    /// <summary>
    ///     Выполняет подключение и аутентификацию к SMPT серверу
    /// </summary>
    public static async Task ConnectAsync(this SmtpClient client, CancellationToken cancellationToken)
    {
        await client.ConnectAsync(_server, _port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_login, _password, cancellationToken);
    }
}
