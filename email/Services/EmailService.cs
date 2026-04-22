using System.Net;
using System.Net.Mail;

namespace Reconciliation.Api.Services
{
    public class EmailService
    {
        public void SendWithAttachment(List<string> toEmails, List<string> filePaths)
        {
            using var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(
                    "yasintadestiy19@gmail.com",
                    "emqn zbjg qgcc pxpc"
                ),
                EnableSsl = true,
            };

            using var mail = new MailMessage
            {
                From = new MailAddress("yasintadestiy19@gmail.com"),
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