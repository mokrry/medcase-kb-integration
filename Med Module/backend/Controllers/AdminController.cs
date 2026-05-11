using System.Reflection;
using ClosedXML.Excel;
using MedicalFeaturePrototype.Api.Data;
using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Entities;
using MedicalFeaturePrototype.Api.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MedicalFeaturePrototype.Api.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private const string SolverUrl = "http://kb.ai-hippocrates.ru:8887/ai-hippocrates-solver/solver/key-value-request/1";

    private readonly ApplicationDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ChatGptOptions> _chatGptOptions;
    private readonly IOptions<GeminiOptions> _geminiOptions;
    private readonly IOptions<GigaChatOptions> _gigaChatOptions;

    public AdminController(
        ApplicationDbContext dbContext,
        IWebHostEnvironment environment,
        IHttpClientFactory httpClientFactory,
        IOptions<ChatGptOptions> chatGptOptions,
        IOptions<GeminiOptions> geminiOptions,
        IOptions<GigaChatOptions> gigaChatOptions)
    {
        _dbContext = dbContext;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
        _chatGptOptions = chatGptOptions;
        _geminiOptions = geminiOptions;
        _gigaChatOptions = gigaChatOptions;
    }

    [HttpGet("knowledge-base")]
    public async Task<ActionResult<AdminKnowledgeBaseStatusDto>> GetKnowledgeBaseStatus(CancellationToken cancellationToken)
    {
        var path = ResolveKnowledgeBasePath();
        var lastSolve = await _dbContext.ProcessingRequests
            .AsNoTracking()
            .Where(request => request.InternalMode == "manual-symptoms-solver")
            .OrderByDescending(request => request.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var result = new AdminKnowledgeBaseStatusDto
        {
            FileFound = path is not null,
            FileName = path is null ? "База_знаний_v24.5.xlsx" : Path.GetFileName(path),
            LastPayloadJson = lastSolve?.SolverPayloadJson ?? string.Empty,
            LastSolverResponseJson = lastSolve?.SolverResponseJson ?? string.Empty
        };

        if (path is not null)
        {
            using var workbook = new XLWorkbook(path);
            result.WorksheetCount = workbook.Worksheets.Count;
            result.KeyTables = workbook.Worksheets
                .Select(worksheet => worksheet.Name)
                .Where(name =>
                    name.Contains("activation_condition", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("node_activation", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("nodes", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("узл", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.PostAsJsonAsync(SolverUrl, new Dictionary<string, string>(), cancellationToken);
            result.SolverAvailable = response.IsSuccessStatusCode;
            result.SolverStatus = $"HTTP {(int)response.StatusCode}";
        }
        catch (Exception exception)
        {
            result.SolverAvailable = false;
            result.SolverStatus = exception.Message;
        }

        return Ok(result);
    }

    [HttpGet("integrations")]
    public async Task<ActionResult<IReadOnlyList<AdminIntegrationStatusDto>>> GetIntegrations(
        [FromQuery] bool check = false,
        CancellationToken cancellationToken = default)
    {
        var chatGpt = _chatGptOptions.Value;
        var gemini = _geminiOptions.Value;
        var gigaChat = _gigaChatOptions.Value;

        var items = new List<AdminIntegrationStatusDto>
        {
            new()
            {
                Provider = "ChatGPT proxy",
                Model = chatGpt.Model,
                BaseUrl = chatGpt.BaseUrl,
                Configured = !string.IsNullOrWhiteSpace(chatGpt.ApiKey),
                KeyStatus = BuildSecretStatus(chatGpt.ApiKey)
            },
            new()
            {
                Provider = "Gemini proxy",
                Model = gemini.Model,
                BaseUrl = gemini.BaseUrl,
                Configured = !string.IsNullOrWhiteSpace(gemini.ApiKey),
                KeyStatus = BuildSecretStatus(gemini.ApiKey)
            },
            new()
            {
                Provider = "GigaChat",
                Model = gigaChat.Model,
                BaseUrl = gigaChat.ApiBaseUrl,
                Configured = !string.IsNullOrWhiteSpace(gigaChat.AuthorizationKey),
                KeyStatus = BuildSecretStatus(gigaChat.AuthorizationKey)
            }
        };

        if (check)
        {
            foreach (var item in items)
            {
                await CheckIntegrationAsync(item, cancellationToken);
            }
        }

        return Ok(items);
    }

    [HttpGet("diagnostics")]
    public async Task<ActionResult<AdminSystemDiagnosticsDto>> GetDiagnostics(CancellationToken cancellationToken)
    {
        var requests = await _dbContext.ProcessingRequests.AsNoTracking().ToListAsync(cancellationToken);
        var lastError = requests
            .Where(request => !string.IsNullOrWhiteSpace(request.ErrorMessage))
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => request.ErrorMessage)
            .FirstOrDefault() ?? string.Empty;

        return Ok(new AdminSystemDiagnosticsDto
        {
            BackendVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
            PostgreSqlAvailable = await _dbContext.Database.CanConnectAsync(cancellationToken),
            TotalRequests = requests.Count,
            CompletedRequests = requests.Count(request => request.Status == ProcessingRequestStatuses.Completed),
            FailedRequests = requests.Count(request => request.Status == ProcessingRequestStatuses.Failed),
            StartedRequests = requests.Count(request => request.Status == ProcessingRequestStatuses.Started),
            LastError = lastError
        });
    }

    private string? ResolveKnowledgeBasePath()
    {
        var dataDirectory = Path.Combine(_environment.ContentRootPath, "Data");
        if (!Directory.Exists(dataDirectory))
        {
            return null;
        }

        return Directory
            .EnumerateFiles(dataDirectory, "*.xlsx", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => Path.GetFileName(path).Contains("База_знаний_v24.5", StringComparison.OrdinalIgnoreCase));
    }

    private async Task CheckIntegrationAsync(AdminIntegrationStatusDto item, CancellationToken cancellationToken)
    {
        if (!item.Configured)
        {
            item.Available = false;
            item.LastCheckResult = "API key is not configured";
            return;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.GetAsync(item.BaseUrl, cancellationToken);
            item.Available = true;
            item.LastCheckResult = $"Endpoint reachable, HTTP {(int)response.StatusCode}";
        }
        catch (Exception exception)
        {
            item.Available = false;
            item.LastCheckResult = exception.Message;
        }
    }

    private static string BuildSecretStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "not set";
        }

        return value.Length <= 8 ? "set" : $"set (...{value[^4..]})";
    }
}
