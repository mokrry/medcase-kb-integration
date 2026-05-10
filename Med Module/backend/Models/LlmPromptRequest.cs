namespace MedicalFeaturePrototype.Api.Models;

public sealed record LlmPromptRequest(
    string Provider,
    string RequestId,
    string Prompt,
    string ComplaintsText,
    IReadOnlyList<string> Symptoms);
