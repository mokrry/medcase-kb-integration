using System.Security.Claims;
using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Services;
using MedicalFeaturePrototype.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalFeaturePrototype.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PromptController : ControllerBase
{
    private readonly IPromptComposerService _promptComposerService;
    private readonly IPromptExecutionService _promptExecutionService;
    private readonly IPromptVotingService _promptVotingService;
    private readonly IKnowledgeBaseSolverService _knowledgeBaseSolverService;
    private readonly IProcessingRequestLogService _requestLogService;

    public PromptController(
        IPromptComposerService promptComposerService,
        IPromptExecutionService promptExecutionService,
        IPromptVotingService promptVotingService,
        IKnowledgeBaseSolverService knowledgeBaseSolverService,
        IProcessingRequestLogService requestLogService)
    {
        _promptComposerService = promptComposerService;
        _promptExecutionService = promptExecutionService;
        _promptVotingService = promptVotingService;
        _knowledgeBaseSolverService = knowledgeBaseSolverService;
        _requestLogService = requestLogService;
    }

    [HttpPost("build")]
    public async Task<ActionResult<PromptBuildResponseDto>> Build(
        [FromForm] PromptBuildRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePromptRequest(request.ComplaintsText, request.SymptomsFile);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var response = await _promptComposerService.BuildPromptAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("run")]
    public async Task<ActionResult<PromptRunResponseDto>> Run(
        [FromForm] PromptRunRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePromptRequest(request.ComplaintsText, request.SymptomsFile);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        if (string.IsNullOrWhiteSpace(PromptExecutionService.NormalizeProvider(request.Provider)))
        {
            return BadRequest(new { message = "Укажите поддерживаемого LLM-провайдера: chatgpt, gemini или gigachat." });
        }

        var response = await _promptExecutionService.ExecuteAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("run-bundle")]
    public async Task<ActionResult<PromptRunBundleResponseDto>> RunBundle(
        [FromForm] PromptRunRequestDto request,
        CancellationToken cancellationToken)
    {
        var validationError = ValidatePromptRequest(request.ComplaintsText, request.SymptomsFile);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var requestedProviders = request.Providers?.Length > 0
            ? request.Providers
            : [request.Provider];

        if (requestedProviders.All(provider =>
                string.IsNullOrWhiteSpace(PromptExecutionService.NormalizeProvider(provider))))
        {
            return BadRequest(new { message = "Укажите хотя бы одного поддерживаемого LLM-провайдера: chatgpt, gemini или gigachat." });
        }

        var userId = GetCurrentUserId();
        request.RequestId = string.IsNullOrWhiteSpace(request.RequestId)
            ? $"PROMPT-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : request.RequestId;

        if (userId is not null)
        {
            await _requestLogService.CreatePromptBundleStartedAsync(
                userId.Value,
                request.RequestId,
                request.ComplaintsText,
                cancellationToken);
        }

        try
        {
            var response = await _promptVotingService.ExecuteBundleAsync(request, cancellationToken);

            if (userId is not null)
            {
                await _requestLogService.CompletePromptBundleAsync(
                    userId.Value,
                    response,
                    cancellationToken);
            }

            return Ok(response);
        }
        catch (Exception exception)
        {
            if (userId is not null)
            {
                await _requestLogService.FailPromptBundleAsync(
                    userId.Value,
                    request.RequestId,
                    request.ComplaintsText,
                    exception.Message,
                    cancellationToken);
            }

            throw;
        }
    }

    [HttpPost("solve-symptoms")]
    public async Task<ActionResult<KnowledgeBaseSolverDto>> SolveSymptoms(
        [FromBody] PromptSolveSymptomsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ComplaintsText))
        {
            return BadRequest(new { message = "Введите медицинский текст." });
        }

        if (request.Symptoms.Count == 0)
        {
            return BadRequest(new { message = "Оставьте хотя бы один симптом для постановки диагноза." });
        }

        var userId = GetCurrentUserId();
        var requestId = $"SOLVE-{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        if (userId is not null)
        {
            await _requestLogService.CreateManualSolveStartedAsync(
                userId.Value,
                requestId,
                request.ComplaintsText,
                request.Symptoms,
                cancellationToken);
        }

        try
        {
            var response = await _knowledgeBaseSolverService.BuildAndSolveAsync(
                request.Symptoms,
                request.ComplaintsText,
                cancellationToken);

            if (userId is not null)
            {
                await _requestLogService.CompleteManualSolveAsync(
                    userId.Value,
                    requestId,
                    request.ComplaintsText,
                    request.Symptoms,
                    response,
                    cancellationToken);
            }

            return Ok(response);
        }
        catch (Exception exception)
        {
            if (userId is not null)
            {
                await _requestLogService.FailManualSolveAsync(
                    userId.Value,
                    requestId,
                    request.ComplaintsText,
                    request.Symptoms,
                    exception.Message,
                    cancellationToken);
            }

            throw;
        }
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static string? ValidatePromptRequest(string complaintsText, IFormFile? symptomsFile)
    {
        if (string.IsNullOrWhiteSpace(complaintsText))
        {
            return "Введите жалобы пациента.";
        }

        if (symptomsFile is not null &&
            symptomsFile.Length > 0 &&
            !symptomsFile.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "Для таблицы симптомов поддерживается только формат XLSX.";
        }

        return null;
    }
}
