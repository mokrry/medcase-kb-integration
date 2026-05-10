namespace MedicalFeaturePrototype.Api.Models;

public sealed record MedicalTextPreparationResult(
    string Provider,
    string Model,
    string Prompt,
    string Content,
    string RawResponse);
