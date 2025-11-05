using BakeryPOS.API.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace BakeryPOS.API.Services
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

        public async Task SendNotificationAsync(string message)
        {
            // Construct the URL for the Telegram Bot API
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

            // Create the payload to send
            var payload = new
            {
                chat_id = _chatId,
                text = message,
                parse_mode = "Markdown" // Optional: allows for formatting like *bold* and _italic_
            };

            var httpClient = _httpClientFactory.CreateClient();
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // Send the request
            var response = await httpClient.PostAsync(url, content);

            // You can add error handling here if you want to check if the message was sent successfully
            // For now, we'll assume it works.
            // response.EnsureSuccessStatusCode();
        }
    }
}