using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MedicalFeaturePrototype.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IExcelDataService _excelDataService;
    private readonly IPatientTextService _patientTextService;
    private readonly IFeatureAnalysisService _featureAnalysisService;

    public PatientsController(
        IExcelDataService excelDataService,
        IPatientTextService patientTextService,
        IFeatureAnalysisService featureAnalysisService)
    {
        _excelDataService = excelDataService;
        _patientTextService = patientTextService;
        _featureAnalysisService = featureAnalysisService;
    }

    [HttpGet]
    public ActionResult<IEnumerable<PatientListItemDto>> GetPatients([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var patients = _excelDataService.LoadPatients()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(patient => new PatientListItemDto
            {
                Id = patient.Id,
                ComplaintsPreview = BuildPreview(patient.Complaints)
            })
            .ToList();

        return Ok(patients);
    }

    [HttpGet("{id:int}")]
    public ActionResult<PatientDetailsDto> GetPatientById(int id)
    {
        var patient = _excelDataService.GetPatientById(id);
        if (patient is null)
        {
            return NotFound(new { message = $"Пациент с id={id} не найден." });
        }

        return Ok(new PatientDetailsDto
        {
            Id = patient.Id,
            Complaints = patient.Complaints,
            Anamnesis = patient.Anamnesis,
            PhysicalExam = patient.PhysicalExam,
            FullText = _patientTextService.BuildFullText(patient)
        });
    }

    [HttpGet("features")]
    public ActionResult<IEnumerable<FeatureDto>> GetFeatures(
        [FromQuery] bool includeComplaintsFeatures = true,
        [FromQuery] bool includeAnamnesisFeatures = true)
    {
        var features = _excelDataService.LoadFeatures(includeComplaintsFeatures, includeAnamnesisFeatures)
            .Select(feature => new FeatureDto
            {
                Name = feature.Name,
                Category = feature.Category
            })
            .ToList();

        return Ok(features);
    }

    [HttpPost("analyze")]
    public ActionResult<PatientAnalysisResponseDto> Analyze([FromBody] PatientAnalysisRequest request)
    {
        if (!request.IncludeComplaintsFeatures && !request.IncludeAnamnesisFeatures)
        {
            return BadRequest(new
            {
                message = "Нужно выбрать хотя бы один словарь признаков для анализа."
            });
        }

        var patient = _excelDataService.GetPatientById(request.PatientId);
        if (patient is null)
        {
            return NotFound(new { message = $"Пациент с id={request.PatientId} не найден." });
        }

        var fullText = _patientTextService.BuildFullText(patient);
        var features = _excelDataService.LoadFeatures(request.IncludeComplaintsFeatures, request.IncludeAnamnesisFeatures);
        var results = _featureAnalysisService.Analyze(fullText, features);

        var response = new PatientAnalysisResponseDto
        {
            PatientId = patient.Id,
            FullText = fullText,
            IncludeComplaintsFeatures = request.IncludeComplaintsFeatures,
            IncludeAnamnesisFeatures = request.IncludeAnamnesisFeatures,
            TotalFeatures = results.Count,
            FoundCount = results.Count(r => r.Status == "Found"),
            NotFoundCount = results.Count(r => r.Status == "NotFound"),
            NeedsReviewCount = results.Count(r => r.Status == "NeedsReview"),
            Results = results.Select(result => new AnalysisResultDto
            {
                FeatureName = result.FeatureName,
                Category = result.Category,
                Status = result.Status,
                Evidence = result.Evidence
            }).ToList()
        };

        return Ok(response);
    }

    private static string BuildPreview(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Нет данных";
        }

        var compact = string.Join(" ", text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 120 ? compact : $"{compact[..120]}...";
    }
}
