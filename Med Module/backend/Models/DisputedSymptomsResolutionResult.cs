namespace MedicalFeaturePrototype.Api.Models;

public sealed record DisputedSymptomsResolutionResult(
    string Provider,
    string Model,
    string Prompt,
    string Content,
    string RawResponse,
    IReadOnlyList<string> CandidateSymptoms,
    IReadOnlyList<string> ConfirmedSymptoms);
