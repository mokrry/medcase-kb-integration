namespace MedicalFeaturePrototype.Api.Models;

public class PatientAnalysisRequest
{
    public int PatientId { get; set; }
    public bool IncludeComplaintsFeatures { get; set; } = true;
    public bool IncludeAnamnesisFeatures { get; set; } = true;
}
