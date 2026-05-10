namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptWorkflowStageDto
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
    public string? Error { get; set; }
}
