using System.Net;
using System.Net.Mail;

namespace TankDesigner.Web.Services
{
    // Servicio encargado de enviar emails usando SMTP
    public class EmailService
    {
        // Se usa IConfiguration para leer datos del appsettings o variables de entorno (Railway)
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Método principal para enviar un email
        public async Task EnviarEmailAsync(string destino, string asunto, string cuerpoHtml)
        {
            // Se obtienen los datos de configuración SMTP
            var host = _configuration["Email:SmtpHost"];
            var portTexto = _configuration["Email:SmtpPort"];
            var user = _configuration["Email:User"];
            var password = _configuration["Email:Password"];
            var fromName = _configuration["Email:FromName"] ?? "Tank Structural Designer";

            // Validación: si falta algo, se lanza error
            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(portTexto) ||
                string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("La configuración SMTP no está completa.");
            }

            // Convierte el puerto a número
            if (!int.TryParse(portTexto, out var port))
                throw new InvalidOperationException("El puerto SMTP no es válido.");

            // Se construye el mensaje de correo
            using var message = new MailMessage
            {
                // Remitente (correo + nombre visible)
                From = new MailAddress(user, fromName),

                // Asunto del email
                Subject = asunto,

                // Cuerpo del email en HTML
                Body = cuerpoHtml,
                IsBodyHtml = true
            };

            // Se añade el destinatario
            message.To.Add(destino);

            // Configuración del cliente SMTP
            using var client = new SmtpClient(host, port)
            {
                // Credenciales del correo
                Credentials = new NetworkCredential(user, password),

                // Se usa SSL (requerido en la mayoría de proveedores)
                EnableSsl = true
            };

            // Envío del email
            await client.SendMailAsync(message);
        }
    }
}