using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TankDesigner.Web.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public EmailService(IConfiguration config)
        {
            _config = config;
            _http = new HttpClient();
        }

        public async Task EnviarEmailAsync(string toEmail, string subject, string body)
        {
            var apiKey = _config["Email:ApiKey"];
            var fromEmail = _config["Email:FromEmail"];
            var fromName = _config["Email:FromName"] ?? "Tank Structural Designer";

            if (string.IsNullOrWhiteSpace(apiKey))
                throw new Exception("Falta Email:ApiKey");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                from = $"{fromName} <{fromEmail}>",
                to = new[] { toEmail },
                subject = subject,
                html = body
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error Resend: {error}");
            }
        }
    }
}