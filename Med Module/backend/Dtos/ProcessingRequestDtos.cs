namespace MedicalFeaturePrototype.Api.Dtos;

public class ProcessingRequestListItemDto
{
    public Guid Id { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string InternalMode { get; set; } = string.Empty;
    public bool UsedVoting { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class ProcessingRequestDetailsDto : ProcessingRequestListItemDto
{
    public string SourceText { get; set; } = string.Empty;
    public string PreparedText { get; set; } = string.Empty;
    public string FinalSymptomsJson { get; set; } = "[]";
    public string EvidenceJson { get; set; } = "{}";
    public string ManualChangesJson { get; set; } = "{}";
    public string SolverPayloadJson { get; set; } = "{}";
    public string SolverResponseJson { get; set; } = string.Empty;
}
