using MedicalFeaturePrototype.Api.Dtos;

namespace MedicalFeaturePrototype.Api.Models;

public sealed record PromptExecutionPayload(
    PromptBuildResponseDto PromptBuild,
    LlmPromptRequest LlmRequest);
