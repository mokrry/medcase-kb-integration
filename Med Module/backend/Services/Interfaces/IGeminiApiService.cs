using MedicalFeaturePrototype.Api.Models;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IGeminiApiService
{
    Task<LlmCompletionResult> GenerateAsync(LlmPromptRequest request, CancellationToken cancellationToken = default);
}
