using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Timers;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Timer = System.Timers.Timer;

namespace MailKit.Web.Controllers
{
    /// <summary>
    /// Пул подключений к SMTP серверу, поддерживающий параллельную отправку писем.
    /// Поддерживает поддержание подключений в открытом состоянии через команду NoOp и 
    /// восстанавливает разорванные соединения при необходимости.
    /// </summary>
    public sealed class SmtpClientPool : IDisposable
    {
        private readonly ConcurrentBag<SmtpClient> _clients;
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer _timer;

        public SmtpClientPool(int connectionCount = 1)
        {
            _clients = new ConcurrentBag<SmtpClient>();
            for (int i = 0; i < connectionCount; i++)
            {
                _clients.Add(new SmtpClient());
            }

            // SemaphoreSlim для управления доступом к пулу соединений.
            _semaphore = new SemaphoreSlim(connectionCount, connectionCount);

            // Таймер для периодической отправки команды NoOp на все соединения.
            _timer = new Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
            _timer.Elapsed += SendNoOp;
            _timer.AutoReset = true;
            _timer.Start();
        }

        /// <summary>
        /// Отправляет письмо, используя одно из SMTP-соединений из пула.
        /// Если соединение не установлено, производится подключение.
        /// В случае возникновения ошибки производится попытка восстановления соединения.
        /// </summary>
        /// <param name="message">Сообщение с письмом</param>
        /// <param name="token">Токен отмены операции</param>
        public async Task SendAsync(MimeMessage message, CancellationToken token)
        {
            // Ждём, когда одно из соединений станет доступным.
            await _semaphore.WaitAsync(token);
            if (!_clients.TryTake(out SmtpClient client))
            {
                _semaphore.Release();
                throw new InvalidOperationException("Нет доступных SMTP клиентов");
            }

            try
            {
                if (!client.IsConnected)
                {
                    await client.EnsureConnectedAsync(token);
                }

                await client.SendAsync(message, token);
            }
            catch (Exception)
            {
                // Если соединение разорвано – пытаемся восстановить его.
                if (!client.IsConnected)
                {
                    await client.EnsureConnectedAsync(token);
                }
                throw;
            }
            finally
            {
                // Возвращаем клиента в пул и освобождаем семафор.
                _clients.Add(client);
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Периодический метод, который проходит по всем соединениям и отправляет команду NoOp для
        /// поддержания их «живыми». Если соединение разорвано, пытается его восстановить.
        /// </summary>
        private async void SendNoOp(object? sender, ElapsedEventArgs e)
        {
            var clientsSnapshot = _clients.ToList();
            var tasks = clientsSnapshot.Select(async client =>
            {
                try
                {
                    if (client.IsConnected)
                    {
                        await client.NoOpAsync();
                    }
                    else
                    {
                        await client.EnsureConnectedAsync(CancellationToken.None);
                    }
                }
                catch
                {
                    // Игнорируем ошибки для отдельного клиента.
                }
            });

            try
            {
                await Task.WhenAll(tasks);
            }
            catch
            {
                // Игнорируем общие ошибки.
            }
        }

        public void Dispose()
        {
            _timer.Elapsed -= SendNoOp;
            _timer.Stop();
            _timer.Dispose();

            while (_clients.TryTake(out var client))
            {
                client.Dispose();
            }
            _semaphore.Dispose();
        }
    }
}
