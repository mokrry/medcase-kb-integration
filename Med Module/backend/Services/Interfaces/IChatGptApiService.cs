using MedicalFeaturePrototype.Api.Models;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IChatGptApiService
{
    Task<LlmCompletionResult> GenerateAsync(LlmPromptRequest request, CancellationToken cancellationToken = default);
}
