using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TankDesigner.Web.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task EnviarEmailAsync(string destino, string asunto, string cuerpoHtml)
        {
            var provider = _configuration["Email:Provider"];

            if (!string.Equals(provider, "Resend", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Proveedor de email no configurado. Usa Email__Provider=Resend.");

            var apiKey = _configuration["Email:ApiKey"];
            var fromEmail = _configuration["Email:FromEmail"];
            var fromName = _configuration["Email:FromName"] ?? "Tank Structural Designer";

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Falta Email__ApiKey.");

            if (string.IsNullOrWhiteSpace(fromEmail))
                throw new InvalidOperationException("Falta Email__FromEmail.");

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                from = $"{fromName} <{fromEmail}>",
                to = new[] { destino },
                subject = asunto,
                html = cuerpoHtml
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error Resend: {StatusCode} - {Body}", response.StatusCode, responseBody);
                throw new Exception($"Error Resend: {responseBody}");
            }
        }
    }
}