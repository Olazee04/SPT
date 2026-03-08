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
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var smtpHost = _config["Email:Host"];
            var smtpPortStr = _config["Email:Port"];
            var smtpUser = _config["Email:User"];
            var smtpPass = _config["Email:Pass"];

            // ✅ Log config values (mask password) so you can debug in Render Logs
            _logger.LogInformation("📧 Email attempt → To: {To} | Host: {Host} | Port: {Port} | User: {User}",
                toEmail, smtpHost, smtpPortStr, smtpUser);

            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
            {
                _logger.LogWarning("⚠️ Email not sent — one or more Email config values are missing or empty.");
                return;
            }

            // ✅ Remove spaces from app password (Gmail shows it with spaces, must be used without)
            smtpPass = smtpPass.Replace(" ", "");

            int smtpPort = int.TryParse(smtpPortStr, out int p) ? p : 587;

            try
            {
                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(smtpUser, "RMSys SPT Academy"),
                    Subject = subject,
                    Body = message,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("✅ Email sent successfully to {To}", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Email failed to {To}: {Message}", toEmail, ex.Message);
                throw; // ✅ Re-throw so callers can catch and show fallback message
            }
        }
    }
}