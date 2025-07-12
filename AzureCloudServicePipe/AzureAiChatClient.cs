using AzureCloudServicePipe.Interfaces;
using AzureCloudServicePipe.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AzureCloudServicePipe
{
    public class AzureAiChatClient : IAzureCloudService
    {

        private readonly HttpClient _httpClient;
        private readonly string _url;
        private readonly ILogger<AzureAiChatClient> _logger;

        public AzureAiChatClient(string apiUrl, string apiKey, ILogger<AzureAiChatClient> logger)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            _url = apiUrl;
            _logger = logger;
        }

        public async Task<string> ChatCompletionSingleAsync(string message, double chatRequestTemperature = 0.0, CancellationToken cancellationToken = default)
        {
            try
            {
                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "user", content = message }
                    },
                    temperature = chatRequestTemperature
                };

                _logger.LogInformation("Sending single chat completion request.");

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_url, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var result = doc.RootElement
                                .GetProperty("choices")[0]
                                .GetProperty("message")
                                .GetProperty("content")
                                .GetString()
                                ?.Trim();

                _logger.LogInformation("Received single chat completion response.");
                return result ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ChatCompletionSingleAsync");
                return "";
            }
        }

        public async Task<string> ChatCompletionMultiAsync(IEnumerable<(string role, string content)> messages, double chatRequestTemperature = 0.0, CancellationToken cancellationToken = default)
        {
            try
            {
                var messageArray = messages.Select(m => new { role = m.role, content = m.content }).ToArray();
                var requestBody = new
                {
                    messages = messageArray,
                    temperature = chatRequestTemperature
                };

                _logger.LogInformation("Sending multi-turn chat completion request.");

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_url, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var result = doc.RootElement
                                .GetProperty("choices")[0]
                                .GetProperty("message")
                                .GetProperty("content")
                                .GetString()
                                ?.Trim();

                _logger.LogInformation("Received multi-turn chat completion response.");
                return result ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ChatCompletionMultiAsync");
                return "";
            }
        }

        public async Task<string> ChatCompletionWithFunctionCallingAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Sending chat completion with function calling.");

                var functions = new[]
                {
                    new
                    {
                        name = "GetWeather",
                        description = "Get the weather for a city",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                city = new
                                {
                                    type = "string",
                                    description = "Name of the city"
                                }
                            },
                            required = new[] { "city" }
                        }
                    }
                };

                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "user", content = userMessage }
                    },
                    functions,
                    function_call = "auto"
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_url, content);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);

                var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message");

                if (message.TryGetProperty("function_call", out var funcCall))
                {
                    var funcName = funcCall.GetProperty("name").GetString();
                    var argumentsJson = funcCall.GetProperty("arguments").GetString();

                    _logger.LogInformation("Function call detected: {FunctionName}", funcName);

                    var functionResult = await CallLocalFunctionAsync(funcName, argumentsJson);

                    var messages = new AzureAiChatMessage[]
                    {
                        new AzureAiChatMessage { role = "user", content = userMessage },
                        new AzureAiChatMessage { role = "function", name = funcName, content = functionResult }
                    };

                    var followupBody = new { messages };
                    var followupContent = new StringContent(JsonSerializer.Serialize(followupBody), Encoding.UTF8, "application/json");
                    var followupResponse = await _httpClient.PostAsync(_url, followupContent);
                    followupResponse.EnsureSuccessStatusCode();

                    var followupString = await followupResponse.Content.ReadAsStringAsync();
                    using var followupDoc = JsonDocument.Parse(followupString);

                    _logger.LogInformation("Follow-up function response processed.");
                    return followupDoc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString()
                        ?.Trim() ?? "";
                }

                _logger.LogInformation("No function call; returning GPT response.");
                return message.GetProperty("content").GetString()?.Trim() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ChatCompletionWithFunctionCallingAsync");
                return "";
            }
        }

        private async Task<string> CallLocalFunctionAsync(string functionName, string argumentsJson)
        {
            try
            {
                switch (functionName)
                {
                    case "GetWeather":
                        using (var doc = JsonDocument.Parse(argumentsJson))
                        {
                            var city = doc.RootElement.GetProperty("city").GetString();
                            _logger.LogInformation("Simulating weather lookup for city: {City}", city);

                            await Task.Delay(500); // simulate latency
                            return $"{{\"temperature\":\"34°C\",\"condition\":\"Sunny\",\"city\":\"{city}\"}}";
                        }

                    default:
                        _logger.LogWarning("Unknown function requested: {FunctionName}", functionName);
                        return "{}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CallLocalFunctionAsync");
                return "{}";
            }
        }

    }
}
