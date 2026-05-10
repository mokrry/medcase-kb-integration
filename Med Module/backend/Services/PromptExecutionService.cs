using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Services.Interfaces;

namespace MedicalFeaturePrototype.Api.Services;

public class PromptExecutionService : IPromptExecutionService
{
    private readonly IPromptComposerService _promptComposerService;
    private readonly IMedicalTextWorkflowService _medicalTextWorkflowService;
    private readonly IChatGptApiService _chatGptApiService;
    private readonly IGeminiApiService _geminiApiService;
    private readonly IGigaChatApiService _gigaChatApiService;
    private readonly ILogger<PromptExecutionService> _logger;

    public PromptExecutionService(
        IPromptComposerService promptComposerService,
        IMedicalTextWorkflowService medicalTextWorkflowService,
        IChatGptApiService chatGptApiService,
        IGeminiApiService geminiApiService,
        IGigaChatApiService gigaChatApiService,
        ILogger<PromptExecutionService> logger)
    {
        _promptComposerService = promptComposerService;
        _medicalTextWorkflowService = medicalTextWorkflowService;
        _chatGptApiService = chatGptApiService;
        _geminiApiService = geminiApiService;
        _gigaChatApiService = gigaChatApiService;
        _logger = logger;
    }

    public async Task<PromptRunResponseDto> ExecuteAsync(PromptRunRequestDto request, CancellationToken cancellationToken = default)
    {
        var preparationRequestId = $"PREP-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var preparationResult = await _medicalTextWorkflowService.PrepareMedicalTextAsync(
            request.ComplaintsText,
            preparationRequestId,
            cancellationToken);

        var executionPayload = await _promptComposerService.BuildExecutionPayloadAsync(
            request,
            preparationResult.Content,
            cancellationToken);
        var llmRequest = executionPayload.LlmRequest;

        _logger.LogInformation(
            "[SERVER] Prompt execution started RequestId={RequestId} Provider={Provider} SymptomsCount={SymptomsCount}",
            llmRequest.RequestId,
            llmRequest.Provider,
            llmRequest.Symptoms.Count);

        var llmResponse = llmRequest.Provider switch
        {
            "chatgpt" => await _chatGptApiService.GenerateAsync(llmRequest, cancellationToken),
            "gemini" => await _geminiApiService.GenerateAsync(llmRequest, cancellationToken),
            "gigachat" => await _gigaChatApiService.GenerateAsync(llmRequest, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported LLM provider: {request.Provider}")
        };

        var normalizedContent = PromptResponseParser.ReorderContentByWhitelist(llmResponse.Content, llmRequest.Symptoms);

        return new PromptRunResponseDto
        {
            PromptBuild = executionPayload.PromptBuild,
            Provider = llmResponse.Provider,
            Model = llmResponse.Model,
            Content = normalizedContent,
            RawResponse = llmResponse.RawResponse
        };
    }

    public static string NormalizeProvider(string? provider)
    {
        return provider?.Trim().ToLowerInvariant() switch
        {
            "chatgpt" or "chat-gpt" or "openai" => "chatgpt",
            "gemini" or "google" => "gemini",
            "gigachat" or "giga-chat" or "giga" => "gigachat",
            _ => string.Empty
        };
    }
}
