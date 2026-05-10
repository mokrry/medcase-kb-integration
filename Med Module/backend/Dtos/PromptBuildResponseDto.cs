namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptBuildResponseDto
{
    public string RequestId { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string ComplaintsText { get; set; } = string.Empty;
    public string SourceComplaintsText { get; set; } = string.Empty;
    public string PreparedComplaintsText { get; set; } = string.Empty;
    public int TotalSymptoms { get; set; }
    public int FilledSymptoms { get; set; }
    public List<SymptomPromptItemDto> Symptoms { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
