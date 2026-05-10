namespace MedicalFeaturePrototype.Api.Dtos;

public class KnowledgeBaseSolverDto
{
    public Dictionary<string, string> Payload { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<KnowledgeBaseMappingDto> Mappings { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string RequestJson { get; set; } = "{}";
    public int? StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string ResponseJson { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public class KnowledgeBaseMappingDto
{
    public string Source { get; set; } = string.Empty;
    public string Symptom { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public string ActivationConditionId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
