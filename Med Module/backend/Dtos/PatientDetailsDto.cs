namespace MedicalFeaturePrototype.Api.Dtos;

public class PatientDetailsDto
{
    public int Id { get; set; }
    public string Complaints { get; set; } = string.Empty;
    public string Anamnesis { get; set; } = string.Empty;
    public string PhysicalExam { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
}
