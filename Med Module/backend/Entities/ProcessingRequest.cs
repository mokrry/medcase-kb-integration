namespace MedicalFeaturePrototype.Api.Entities;

public class ProcessingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string Status { get; set; } = ProcessingRequestStatuses.Started;
    public string InternalMode { get; set; } = "llm-voting";
    public bool UsedVoting { get; set; }
    public string SourceText { get; set; } = string.Empty;
    public string PreparedText { get; set; } = string.Empty;
    public string FinalSymptomsJson { get; set; } = "[]";
    public string EvidenceJson { get; set; } = "{}";
    public string ManualChangesJson { get; set; } = "{}";
    public string SolverPayloadJson { get; set; } = "{}";
    public string SolverResponseJson { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}

public static class ProcessingRequestStatuses
{
    public const string Started = "Started";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}
