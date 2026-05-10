namespace MedicalFeaturePrototype.Api.Models;

public sealed record LlmCompletionResult(
    string Provider,
    string Model,
    string Content,
    string RawResponse);
