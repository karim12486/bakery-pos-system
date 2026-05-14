using Nizam.Api.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace Nizam.Api.Services
{
    public class TelegramNotificationService : INotificationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _botToken;
        private readonly string _chatId;

        public TelegramNotificationService(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _botToken = config["TelegramSettings:BotToken"];
            _chatId = config["TelegramSettings:ChatId"];
        }

        public async Task SendNotificationAsync(string caption, string? filePath = null)
        {
            var httpClient = _httpClientFactory.CreateClient();

            // If a file path is provided, send it as a document
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                var url = $"https://api.telegram.org/bot{_botToken}/sendDocument";

                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(_chatId), "chat_id");
                form.Add(new StringContent(caption), "caption");

                await using var fileStream = File.OpenRead(filePath);
                form.Add(new StreamContent(fileStream), "document", Path.GetFileName(filePath));

                var response = await httpClient.PostAsync(url, form);
                // Optional: check response.IsSuccessStatusCode
            }
            else // Otherwise, just send a text message
            {
                var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";
                var payload = new { chat_id = _chatId, text = caption };
                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(url, content);
            }
        }
    }
}