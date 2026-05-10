using MedicalFeaturePrototype.Api.Dtos;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IPromptVotingService
{
    Task<PromptRunBundleResponseDto> ExecuteBundleAsync(PromptRunRequestDto request, CancellationToken cancellationToken = default);
}
