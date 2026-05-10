using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Options;
using MedicalFeaturePrototype.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedicalFeaturePrototype.Api.Services;

public class GigaChatApiService : IGigaChatApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IGigaChatTokenService _tokenService;
    private readonly GigaChatOptions _options;
    private readonly ILogger<GigaChatApiService> _logger;

    public GigaChatApiService(
        HttpClient httpClient,
        IGigaChatTokenService tokenService,
        IOptions<GigaChatOptions> options,
        ILogger<GigaChatApiService> logger)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmCompletionResult> GenerateAsync(LlmPromptRequest requestPayload, CancellationToken cancellationToken = default)
    {
        ValidateConfiguration(_options);

        var accessToken = await _tokenService.GetAccessTokenAsync(cancellationToken);
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
            },
            stream = false,
            n = 1,
            max_tokens = 512
        }, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(_options.ApiBaseUrl, "/v1/chat/completions"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "[SERVER] GigaChat request sent RequestId={RequestId} Model={Model}",
            requestPayload.RequestId,
            _options.Model);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GigaChat API error {(int)response.StatusCode}: {rawResponse}");
        }

        var content = ExtractGigaChatText(rawResponse);
        _logger.LogInformation(
            "[SERVER] GigaChat response received RequestId={RequestId} Model={Model}",
            requestPayload.RequestId,
            _options.Model);

        return new LlmCompletionResult("gigachat", _options.Model, content, rawResponse);
    }

    private static string ExtractGigaChatText(string rawResponse)
    {
        using var document = JsonDocument.Parse(rawResponse);
        if (!document.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String)
            {
                return content.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string BuildUri(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}{path}";
    }

    private static void ValidateConfiguration(GigaChatOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException("GigaChat model is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl))
        {
            throw new InvalidOperationException("GigaChat API base URL is not configured.");
        }
    }
}
