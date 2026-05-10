using System.Net.Http.Headers;
using System.Text.Json;
using MedicalFeaturePrototype.Api.Options;
using MedicalFeaturePrototype.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedicalFeaturePrototype.Api.Services;

public class GigaChatTokenService : IGigaChatTokenService
{
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(2);

    private readonly HttpClient _httpClient;
    private readonly GigaChatOptions _options;
    private readonly ILogger<GigaChatTokenService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public GigaChatTokenService(
        HttpClient httpClient,
        IOptions<GigaChatOptions> options,
        ILogger<GigaChatTokenService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (HasValidToken())
        {
            return _accessToken!;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (HasValidToken())
            {
                return _accessToken!;
            }

            ValidateConfiguration(_options);

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(_options.AuthBaseUrl, "/api/v2/oauth"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("RqUID", Guid.NewGuid().ToString());
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _options.AuthorizationKey);
            request.Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("scope", _options.Scope)
            ]);

            _logger.LogInformation("[SERVER] GigaChat token request started Scope={Scope}", _options.Scope);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"GigaChat auth error {(int)response.StatusCode}: {rawResponse}");
            }

            using var document = JsonDocument.Parse(rawResponse);
            _accessToken = document.RootElement.GetProperty("access_token").GetString()
                           ?? throw new InvalidOperationException("GigaChat auth returned empty access_token.");

            var expiresAtUnix = document.RootElement.GetProperty("expires_at").GetInt64();
            _expiresAt = ParseUnixTimestamp(expiresAtUnix);

            _logger.LogInformation("[SERVER] GigaChat token updated ExpiresAtUtc={ExpiresAtUtc}", _expiresAt.UtcDateTime);
            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool HasValidToken()
    {
        return !string.IsNullOrWhiteSpace(_accessToken) &&
               _expiresAt > DateTimeOffset.UtcNow.Add(RefreshBuffer);
    }

    private static string BuildUri(string baseUrl, string path)
    {
        return $"{baseUrl.TrimEnd('/')}{path}";
    }

    private static DateTimeOffset ParseUnixTimestamp(long value)
    {
        // GigaChat returns expires_at in Unix milliseconds.
        // Keep a seconds fallback to tolerate unexpected payload variants.
        return value > 253402300799
            ? DateTimeOffset.FromUnixTimeMilliseconds(value)
            : DateTimeOffset.FromUnixTimeSeconds(value);
    }

    private static void ValidateConfiguration(GigaChatOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AuthorizationKey))
        {
            throw new InvalidOperationException("GigaChat authorization key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Scope))
        {
            throw new InvalidOperationException("GigaChat scope is not configured.");
        }
    }
}
