using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace MailKit.Web.Controllers
{
    public static class SmtpClientExtensions
    {
        private const string _server = "1.1.1.1";
        private const string _login = "user@server.com";
        private const string _password = "password";
        private const int _port = 587;

        /// <summary>
        /// Выполняет подключение и аутентификацию к SMTP серверу.
		/// Вынесение SmtpClientExtensions в отдельный файл даёт несколько преимуществ:
		/// 1. Улучшенная читаемость и поддерживаемость. Вынесение в отдельный файл делает код чище, упрощает поиск и поддержку.
		/// 2. Переиспользование кода. Сейчас расширение EnsureConnectedAsync находится в SmtpClientExtensions.cs, что позволяет использовать его в любом месте проекта.
		/// 3. Разделение ответственности. Каждый класс должен выполнять одну задачу. SmtpClientPool управляет пулом соединений. SmtpClientExtensions содержит методы расширения для работы с SmtpClient.
		/// 4. Улучшенная тестируемость. Тесты можно писать отдельно, не затрагивая SmtpClientPool. Это упрощает юнит-тестирование.
        /// </summary>
        public static async Task EnsureConnectedAsync(this SmtpClient client, CancellationToken cancellationToken)
        {
            client.ServerCertificateValidationCallback=(s,c,h,e)=>true;
            await client.ConnectAsync(_server, _port, SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(_login, _password, cancellationToken);
        }
    }
}