namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptResponseReviewDto
{
    public bool IsCompliant { get; set; }
    public bool HasCriticalIssues { get; set; }
    public int Score { get; set; }
    public List<string> ExtractedSymptoms { get; set; } = [];
    public List<string> Issues { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
