namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptRunResponseDto
{
    public PromptBuildResponseDto PromptBuild { get; set; } = new();
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
}
