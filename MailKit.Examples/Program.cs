using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace MailKit.Examples;

internal class Program
{
    static async Task Main(string[] args)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("DK", "1@gmail.com"));
        message.To.Add(new MailboxAddress("DK", "2@mail.ru"));
        message.Subject = "TEST";

        message.Body = new TextPart(TextFormat.Plain)
        {
            Text = @"TEST"
        };

        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var token = cancellationTokenSource.Token;
    
        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls, token);
        await client.AuthenticateAsync("1@gmail.com", "not my real app password", token);

        lock (client.SyncRoot)
        {
            client.Send(message);
        }

        await client.DisconnectAsync(true, token);

        Console.WriteLine("Email has been sent. Press any key to exit...");
        Console.ReadKey();
    }
}