namespace MedicalFeaturePrototype.Api.Dtos;

public class SymptomEvidenceVerificationDto
{
    public List<SymptomEvidenceDto> Symptoms { get; set; } = [];
    public PromptWorkflowStageDto Stage { get; set; } = new();
}

public class SymptomEvidenceDto
{
    public string Name { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public int? EvidenceStart { get; set; }
    public int? EvidenceEnd { get; set; }
    public string VerificationStatus { get; set; } = "needsReview";
}
