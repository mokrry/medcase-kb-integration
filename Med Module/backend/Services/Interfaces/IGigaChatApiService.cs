using MedicalFeaturePrototype.Api.Models;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IGigaChatApiService
{
    Task<LlmCompletionResult> GenerateAsync(LlmPromptRequest request, CancellationToken cancellationToken = default);
}
