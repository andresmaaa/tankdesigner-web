using System.Net;
using System.Net.Mail;
using System.Text;

namespace TankDesigner.Web.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task EnviarEmailAsync(string destino, string asunto, string cuerpoHtml)
        {
            if (string.IsNullOrWhiteSpace(destino))
                throw new ArgumentException("El email de destino no puede estar vacío.", nameof(destino));

            string smtpHost = ObtenerConfig("EmailSettings:SmtpHost");
            int smtpPort = ObtenerIntConfig("EmailSettings:SmtpPort", 587);
            string smtpUser = ObtenerConfig("EmailSettings:SmtpUser");
            string smtpPassword = ObtenerConfig("EmailSettings:SmtpPassword");
            string fromEmail = ObtenerConfig("EmailSettings:FromEmail");
            string fromName = ObtenerConfig("EmailSettings:FromName", "Tank Structural Designer");
            bool enableSsl = ObtenerBoolConfig("EmailSettings:EnableSsl", true);

            if (string.IsNullOrWhiteSpace(smtpHost))
                throw new InvalidOperationException("Falta EmailSettings__SmtpHost.");

            if (string.IsNullOrWhiteSpace(smtpUser))
                throw new InvalidOperationException("Falta EmailSettings__SmtpUser.");

            if (string.IsNullOrWhiteSpace(smtpPassword))
                throw new InvalidOperationException("Falta EmailSettings__SmtpPassword.");

            if (string.IsNullOrWhiteSpace(fromEmail))
                fromEmail = smtpUser;

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName, Encoding.UTF8),
                Subject = asunto,
                SubjectEncoding = Encoding.UTF8,
                Body = cuerpoHtml,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };

            message.To.Add(new MailAddress(destino));

            using var smtp = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPassword),
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            try
            {
                await smtp.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email SMTP a {Destino}", destino);
                throw;
            }
        }

        private string ObtenerConfig(string key, string defaultValue = "")
        {
            return _configuration[key]?.Trim() ?? defaultValue;
        }

        private int ObtenerIntConfig(string key, int defaultValue)
        {
            return int.TryParse(_configuration[key], out int value) ? value : defaultValue;
        }

        private bool ObtenerBoolConfig(string key, bool defaultValue)
        {
            return bool.TryParse(_configuration[key], out bool value) ? value : defaultValue;
        }
    }
}