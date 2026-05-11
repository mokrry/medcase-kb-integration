namespace MedicalFeaturePrototype.Api.Dtos;

public class AdminKnowledgeBaseStatusDto
{
    public bool FileFound { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int WorksheetCount { get; set; }
    public List<string> KeyTables { get; set; } = [];
    public bool SolverAvailable { get; set; }
    public string SolverStatus { get; set; } = string.Empty;
    public string LastPayloadJson { get; set; } = string.Empty;
    public string LastSolverResponseJson { get; set; } = string.Empty;
}

public class AdminIntegrationStatusDto
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool Configured { get; set; }
    public string KeyStatus { get; set; } = string.Empty;
    public bool? Available { get; set; }
    public string LastCheckResult { get; set; } = string.Empty;
}

public class AdminSystemDiagnosticsDto
{
    public string BackendVersion { get; set; } = string.Empty;
    public bool PostgreSqlAvailable { get; set; }
    public string ApiStatus { get; set; } = "available";
    public int TotalRequests { get; set; }
    public int CompletedRequests { get; set; }
    public int FailedRequests { get; set; }
    public int StartedRequests { get; set; }
    public string LastError { get; set; } = string.Empty;
}
