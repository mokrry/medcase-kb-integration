using MedicalFeaturePrototype.Api.Dtos;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IPromptExecutionService
{
    Task<PromptRunResponseDto> ExecuteAsync(PromptRunRequestDto request, CancellationToken cancellationToken = default);
}
