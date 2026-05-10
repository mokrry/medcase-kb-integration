using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Options;
using MedicalFeaturePrototype.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedicalFeaturePrototype.Api.Services;

public class ChatGptApiService : IChatGptApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ChatGptOptions _options;
    private readonly ILogger<ChatGptApiService> _logger;

    public ChatGptApiService(
        HttpClient httpClient,
        IOptions<ChatGptOptions> options,
        ILogger<ChatGptApiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmCompletionResult> GenerateAsync(LlmPromptRequest requestPayload, CancellationToken cancellationToken = default)
    {
        ValidateConfiguration(_options.ApiKey, "ChatGPT");

        var requestBody = JsonSerializer.Serialize(new
        {
            model = _options.Model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = requestPayload.Prompt
                }
            }
        }, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(_options.BaseUrl, "/v1/chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "[SERVER] ChatGPT request sent RequestId={RequestId} Model={Model}",
            requestPayload.RequestId,
            _options.Model);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"ChatGPT API error {(int)response.StatusCode}: {rawResponse}");
        }

        var content = ExtractOpenAiText(rawResponse);
        _logger.LogInformation(
            "[SERVER] ChatGPT response received RequestId={RequestId} Model={Model}",
            requestPayload.RequestId,
            _options.Model);

        return new LlmCompletionResult("chatgpt", _options.Model, content, rawResponse);
    }

    private static string ExtractOpenAiText(string rawResponse)
    {
        using var document = JsonDocument.Parse(rawResponse);
        if (document.RootElement.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array)
        {
            foreach (var choice in choices.EnumerateArray())
            {
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.String)
                {
                    return content.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private static string BuildUri(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}{path}";
    }

    private static void ValidateConfiguration(string apiKey, string providerName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"{providerName} API key is not configured.");
        }
    }
}
