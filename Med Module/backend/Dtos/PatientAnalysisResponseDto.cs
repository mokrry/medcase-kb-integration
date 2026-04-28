namespace MedicalFeaturePrototype.Api.Dtos;

public class PatientAnalysisResponseDto
{
    public int PatientId { get; set; }
    public string FullText { get; set; } = string.Empty;
    public bool IncludeComplaintsFeatures { get; set; }
    public bool IncludeAnamnesisFeatures { get; set; }
    public int TotalFeatures { get; set; }
    public int FoundCount { get; set; }
    public int NotFoundCount { get; set; }
    public int NeedsReviewCount { get; set; }
    public List<AnalysisResultDto> Results { get; set; } = new();
}
