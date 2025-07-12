
using AzureCloudServicePipe.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AzureCloudServicePipe
{
    public class AzureAiImageClient: IAzureCloudService
    {
        private readonly HttpClient _httpClient;
        private readonly string _url;
        private readonly string _apiKey;
        private readonly ILogger<AzureAiImageClient> _logger;

        public AzureAiImageClient(string apiUrl, string apiKey, ILogger<AzureAiImageClient> logger)
        {
            _apiKey = apiKey;
            _url = apiUrl;
            _logger = logger;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public async Task<string> GenerateImageWithPollingAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var url = _url;
            var requestBody = new
            {
                prompt = prompt,
                n = 1,
                size = "1024x1024"
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                _logger.LogInformation("Submitting image generation request for prompt: {Prompt}", prompt);

                // Optional delay before polling
                await Task.Delay(500);

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Image generation failed. Status: {StatusCode}, Error: {Error}", response.StatusCode, error);
                    throw new HttpRequestException($"Image generation failed: {response.StatusCode}\n{error}");
                }

                _logger.LogInformation("Waiting for image generation to complete...");
                await Task.Delay(2500); // Simulate polling

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var imageUrl = doc.RootElement
                                  .GetProperty("data")[0]
                                  .GetProperty("url")
                                  .GetString();

                _logger.LogInformation("Image successfully generated: {ImageUrl}", imageUrl);

                return imageUrl!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while generating the image.");
                throw;
            }
        }

    }
}
