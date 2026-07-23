using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace StallmedManager.Server.Services
{
    // Αποστολή email με συνημμένο PDF μέσω SMTP.
    // Ρυθμίσεις στο appsettings.json, section "Smtp". Κάθε εταιρεία (SM/BM)
    // έχει δικό της λογαριασμό αποστολής: τα πεδία διαβάζονται πρώτα από το
    // "Smtp:{company}" και, αν λείπουν, από το κοινό "Smtp" (fallback) --
    // έτσι κοινά πράγματα (π.χ. Host/Port) γράφονται μία φορά.
    public class QuoteEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<QuoteEmailService> _logger;

        public QuoteEmailService(IConfiguration config, ILogger<QuoteEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        private string? Setting(string company, string key)
            => _config[$"Smtp:{company}:{key}"] ?? _config[$"Smtp:{key}"];

        public bool IsConfigured(string company) =>
            !string.IsNullOrEmpty(Setting(company, "Host")) &&
            !string.IsNullOrEmpty(Setting(company, "FromAddress"));

        public async Task SendAsync(string company, string toAddress, string? toName, string subject, string body, byte[] pdfBytes, string pdfFileName)
        {
            var host = Setting(company, "Host");
            var port = int.TryParse(Setting(company, "Port"), out var p) ? p : 587;
            var username = Setting(company, "Username");
            var password = Setting(company, "Password");
            var fromAddress = Setting(company, "FromAddress");
            var fromName = Setting(company, "FromName") ?? fromAddress;
            var useSsl = !string.Equals(Setting(company, "UseSsl"), "false", StringComparison.OrdinalIgnoreCase);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromAddress));
            message.To.Add(new MailboxAddress(toName ?? toAddress, toAddress));
            message.Subject = subject;

            var builder = new BodyBuilder { TextBody = body };
            builder.Attachments.Add(pdfFileName, pdfBytes, new ContentType("application", "pdf"));
            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, useSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None);
            if (!string.IsNullOrEmpty(username))
                await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Εστάλη email προσφοράς ({Company}) από {From} προς {To} με θέμα '{Subject}'",
                company, fromAddress, toAddress, subject);
        }
    }
}
