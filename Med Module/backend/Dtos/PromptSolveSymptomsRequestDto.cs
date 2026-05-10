namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptSolveSymptomsRequestDto
{
    public string ComplaintsText { get; set; } = string.Empty;
    public List<string> Symptoms { get; set; } = [];
}
