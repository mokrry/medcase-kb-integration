namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptRunBundleResponseDto
{
    public PromptBuildResponseDto PromptBuild { get; set; } = new();
    public PromptWorkflowStageDto Preparation { get; set; } = new();
    public List<PromptProviderExecutionDto> Results { get; set; } = [];
    public PromptDisputeResolutionDto DisputeResolution { get; set; } = new();
    public PromptVotingSummaryDto Voting { get; set; } = new();
    public SymptomEvidenceVerificationDto EvidenceVerification { get; set; } = new();
    public KnowledgeBaseSolverDto Solver { get; set; } = new();
}
