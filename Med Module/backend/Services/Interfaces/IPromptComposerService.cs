using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Models;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IPromptComposerService
{
    Task<PromptBuildResponseDto> BuildPromptAsync(PromptBuildRequestDto request, CancellationToken cancellationToken = default);
    Task<PromptExecutionPayload> BuildExecutionPayloadAsync(PromptRunRequestDto request, CancellationToken cancellationToken = default);
    Task<PromptExecutionPayload> BuildExecutionPayloadAsync(PromptRunRequestDto request, string preparedComplaintsText, CancellationToken cancellationToken = default);
}
