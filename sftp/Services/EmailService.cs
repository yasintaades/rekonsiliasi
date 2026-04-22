using System.Net;
using System.Net.Mail;

namespace Reconciliation.Api.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void SendWithAttachment(List<string> toEmails, List<string> filePaths)
        {
            var host = _configuration["EmailSettings:Host"] ?? "";
            var portValue = _configuration["EmailSettings:Port"];
            var enableSslValue = _configuration["EmailSettings:EnableSsl"];
            var username = _configuration["EmailSettings:Username"] ?? "";
            var password = _configuration["EmailSettings:Password"] ?? "";
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? username;
            var senderName = _configuration["EmailSettings:SenderName"] ?? "";

            _ = int.TryParse(portValue, out var port);
            _ = bool.TryParse(enableSslValue, out var enableSsl);

            using var smtpClient = new SmtpClient(host)
            {
                Port = port == 0 ? 587 : port,
                Credentials = new NetworkCredential(username, password),
                EnableSsl = enableSsl,
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = "RECONCILIATION RESULT - MIS-MATCH NOTIFICATION",
                Body = "Silakan lihat file terlampir.",
                IsBodyHtml = false
            };

            // multiple recipients
            foreach (var email in toEmails)
            {
                mail.To.Add(email);
            }

            // multiple attachments
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    mail.Attachments.Add(new Attachment(path));
                }
            }

            smtpClient.Send(mail);
        }
    }
}