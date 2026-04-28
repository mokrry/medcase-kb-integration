namespace MedicalFeaturePrototype.Api.Dtos;

public class AnalysisResultDto
{
    public string FeatureName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
}
