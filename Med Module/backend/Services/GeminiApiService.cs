using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Options;
using MedicalFeaturePrototype.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedicalFeaturePrototype.Api.Services;

public class GeminiApiService : IGeminiApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiApiService> _logger;

    public GeminiApiService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiApiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmCompletionResult> GenerateAsync(LlmPromptRequest requestPayload, CancellationToken cancellationToken = default)
    {
        ValidateConfiguration(_options.ApiKey, "Gemini");

        var requestBody = JsonSerializer.Serialize(new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = requestPayload.Prompt
                        }
                    }
                }
            }
        }, JsonOptions);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri(_options.BaseUrl, $"/v1beta/models/{_options.Model}:generateContent"));
        ApplyAuthenticationHeader(request, _options);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "[SERVER] Gemini request sent RequestId={RequestId} Model={Model}",
            requestPayload.RequestId,
            _options.Model);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Gemini API error {(int)response.StatusCode}: {rawResponse}");
        }

        var content = ExtractGeminiText(rawResponse);
        _logger.LogInformation(
            "[SERVER] Gemini response received RequestId={RequestId} Model={Model}",
            requestPayload.RequestId,
            _options.Model);

        return new LlmCompletionResult("gemini", _options.Model, content, rawResponse);
    }

    private static string ExtractGeminiText(string rawResponse)
    {
        using var document = JsonDocument.Parse(rawResponse);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var builder = new StringBuilder();
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    builder.Append(text.GetString());
                }
            }

            if (builder.Length > 0)
            {
                return builder.ToString();
            }
        }

        return string.Empty;
    }

    private static string BuildUri(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}{path}";
    }

    private static void ApplyAuthenticationHeader(HttpRequestMessage request, GeminiOptions options)
    {
        if (IsProxyApiBaseUrl(options.BaseUrl))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            return;
        }

        request.Headers.Add("x-goog-api-key", options.ApiKey);
    }

    private static bool IsProxyApiBaseUrl(string baseUrl)
    {
        return baseUrl.Contains("proxyapi.ru", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateConfiguration(string apiKey, string providerName)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"{providerName} API key is not configured.");
        }
    }
}
