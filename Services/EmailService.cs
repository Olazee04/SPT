using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace SPT.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string message);
    }

    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public SmtpEmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            // Fetch settings from appsettings.json
            var smtpHost = _config["Email:Host"];
            var smtpPort = int.Parse(_config["Email:Port"]);
            var smtpUser = _config["Email:User"];
            var smtpPass = _config["Email:Pass"];

            var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpUser, "RMSys SPT Academy"),
                Subject = subject,
                Body = message,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            try
            {
                await client.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                // Log error or ignore in dev mode
                Console.WriteLine($"Email failed: {ex.Message}");
            }
        }
    }
}