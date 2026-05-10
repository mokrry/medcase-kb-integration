using Microsoft.AspNetCore.Http;

namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptBuildRequestDto
{
    public string ComplaintsText { get; set; } = string.Empty;
    public IFormFile? SymptomsFile { get; set; }
}
