namespace MedicalFeaturePrototype.Api.Models;

public class PatientRecord
{
    public int Id { get; set; }
    public string Complaints { get; set; } = string.Empty;
    public string Anamnesis { get; set; } = string.Empty;
    public string PhysicalExam { get; set; } = string.Empty;
}
