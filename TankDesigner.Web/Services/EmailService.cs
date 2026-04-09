using System.Net;
using System.Net.Mail;

namespace TankDesigner.Web.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task EnviarEmailAsync(string destino, string asunto, string cuerpoHtml)
        {
            var host = _configuration["Email:SmtpHost"];
            var portTexto = _configuration["Email:SmtpPort"];
            var user = _configuration["Email:User"];
            var password = _configuration["Email:Password"];
            var fromName = _configuration["Email:FromName"] ?? "Tank Structural Designer";

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(portTexto) ||
                string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("La configuración SMTP no está completa.");
            }

            if (!int.TryParse(portTexto, out var port))
                throw new InvalidOperationException("El puerto SMTP no es válido.");

            using var message = new MailMessage
            {
                From = new MailAddress(user, fromName),
                Subject = asunto,
                Body = cuerpoHtml,
                IsBodyHtml = true
            };

            message.To.Add(destino);

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(user, password),
                EnableSsl = true
            };

            await client.SendMailAsync(message);
        }
    }
}