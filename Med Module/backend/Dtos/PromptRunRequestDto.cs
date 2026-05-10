using Microsoft.AspNetCore.Http;

namespace MedicalFeaturePrototype.Api.Dtos;

public class PromptRunRequestDto
{
    public string RequestId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string[]? Providers { get; set; }
    public string ComplaintsText { get; set; } = string.Empty;
    public IFormFile? SymptomsFile { get; set; }
}
