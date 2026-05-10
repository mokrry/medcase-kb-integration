namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptDisputeResolutionDto
{
    public List<string> CandidateSymptoms { get; set; } = [];
    public List<string> ConfirmedSymptoms { get; set; } = [];
    public PromptWorkflowStageDto Stage { get; set; } = new();
}
