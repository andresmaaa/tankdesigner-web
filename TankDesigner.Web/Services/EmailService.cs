using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TankDesigner.Web.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly HttpClient _httpClient;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger,
            HttpClient httpClient)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task EnviarEmailAsync(string destino, string asunto, string cuerpoHtml)
        {
            if (string.IsNullOrWhiteSpace(destino))
                throw new ArgumentException("El email de destino no puede estar vacío.", nameof(destino));

            string apiKey = ObtenerConfig("Email:ApiKey");
            string fromEmail = ObtenerConfig("Email:FromEmail", "onboarding@resend.dev");
            string fromName = ObtenerConfig("Email:FromName", "Tank Structural Designer");

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Falta Email__ApiKey en Railway.");

            var payload = new
            {
                from = $"{fromName} <{fromEmail}>",
                to = new[] { destino },
                subject = asunto,
                html = cuerpoHtml
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Error enviando email con Resend a {Destino}. Status: {StatusCode}. Body: {Body}",
                    destino,
                    response.StatusCode,
                    responseBody);

                throw new InvalidOperationException($"Error Resend: {response.StatusCode} - {responseBody}");
            }

            _logger.LogInformation("Email enviado correctamente con Resend a {Destino}", destino);
        }

        private string ObtenerConfig(string key, string defaultValue = "")
        {
            return _configuration[key]?.Trim() ?? defaultValue;
        }
    }
}