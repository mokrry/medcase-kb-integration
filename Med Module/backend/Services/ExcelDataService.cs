using ClosedXML.Excel;
using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Services.Interfaces;

namespace MedicalFeaturePrototype.Api.Services;

public class ExcelDataService : IExcelDataService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly object _sync = new();
    private IReadOnlyList<PatientRecord>? _patientsCache;
    private IReadOnlyList<TargetFeature>? _complaintFeaturesCache;
    private IReadOnlyList<TargetFeature>? _anamnesisFeaturesCache;

    public ExcelDataService(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public IReadOnlyList<PatientRecord> LoadPatients()
    {
        EnsureLoaded();
        return _patientsCache!;
    }

    public PatientRecord? GetPatientById(int id)
    {
        EnsureLoaded();
        return _patientsCache!.FirstOrDefault(x => x.Id == id);
    }

    public IReadOnlyList<TargetFeature> LoadFeatures(bool includeComplaintsFeatures, bool includeAnamnesisFeatures)
    {
        EnsureLoaded();

        var result = new List<TargetFeature>();

        if (includeComplaintsFeatures)
        {
            result.AddRange(_complaintFeaturesCache!);
        }

        if (includeAnamnesisFeatures)
        {
            result.AddRange(_anamnesisFeaturesCache!);
        }

        return result;
    }

    private void EnsureLoaded()
    {
        if (_patientsCache is not null && _complaintFeaturesCache is not null && _anamnesisFeaturesCache is not null)
        {
            return;
        }

        lock (_sync)
        {
            if (_patientsCache is not null && _complaintFeaturesCache is not null && _anamnesisFeaturesCache is not null)
            {
                return;
            }

            var filePath = ResolveExcelPath();

            using var workbook = new XLWorkbook(filePath);

            _patientsCache = LoadPatientsInternal(workbook);
            _complaintFeaturesCache = LoadSingleColumnFeatures(workbook, "Интересующие жалобы", "Complaint");
            _anamnesisFeaturesCache = LoadSingleColumnFeatures(workbook, "Дополнительные данные из анамне", "Anamnesis");
        }
    }

    private string ResolveExcelPath()
    {
        var relativePath = _configuration["Excel:FilePath"] ?? "Data/Для студента.xlsx";
        return Path.Combine(_environment.ContentRootPath, relativePath);
    }

    private static IReadOnlyList<PatientRecord> LoadPatientsInternal(XLWorkbook workbook)
    {
        var sheet = workbook.Worksheet("Сводные данные");
        var usedRange = sheet.RangeUsed();

        if (usedRange is null)
        {
            return Array.Empty<PatientRecord>();
        }

        var headerRow = usedRange.FirstRow();
        var headers = headerRow.Cells().Select((cell, index) => new
        {
            Index = index + 1,
            Value = NormalizeHeader(cell.GetString())
        }).ToList();

        var complaintsColumn = headers.FirstOrDefault(h => h.Value.Contains("жалобы"))?.Index;
        var anamnesisColumn = headers.FirstOrDefault(h => h.Value.Contains("анамнез заболевания"))?.Index;
        var physicalExamColumn = headers.FirstOrDefault(h => h.Value.Contains("физикальное обследование"))?.Index;
        var idColumn = headers.FirstOrDefault(h => string.IsNullOrWhiteSpace(h.Value))?.Index ?? 1;

        if (complaintsColumn is null || anamnesisColumn is null || physicalExamColumn is null)
        {
            throw new InvalidOperationException("Не удалось найти обязательные колонки в листе 'Сводные данные'.");
        }

        var patients = new List<PatientRecord>();

        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            var complaints = row.Cell(complaintsColumn.Value).GetString().Trim();
            var anamnesis = row.Cell(anamnesisColumn.Value).GetString().Trim();
            var physicalExam = row.Cell(physicalExamColumn.Value).GetString().Trim();

            if (string.IsNullOrWhiteSpace(complaints) &&
                string.IsNullOrWhiteSpace(anamnesis) &&
                string.IsNullOrWhiteSpace(physicalExam))
            {
                continue;
            }

            var rawId = row.Cell(idColumn).GetString().Trim();
            var parsed = int.TryParse(rawId, out var id);

            patients.Add(new PatientRecord
            {
                Id = parsed ? id : patients.Count,
                Complaints = complaints,
                Anamnesis = anamnesis,
                PhysicalExam = physicalExam
            });
        }

        return patients;
    }

    private static IReadOnlyList<TargetFeature> LoadSingleColumnFeatures(XLWorkbook workbook, string worksheetName, string category)
    {
        var sheet = workbook.Worksheet(worksheetName);
        var usedRange = sheet.RangeUsed();

        if (usedRange is null)
        {
            return Array.Empty<TargetFeature>();
        }

        var features = new List<TargetFeature>();
        var featureId = 1;

        foreach (var row in usedRange.RowsUsed())
        {
            var value = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            features.Add(new TargetFeature
            {
                Id = featureId++,
                Name = value,
                Category = category
            });
        }

        return features
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select((group, index) => new TargetFeature
            {
                Id = index + 1,
                Name = group.First().Name.Trim(),
                Category = category
            })
            .ToList();
    }

    private static string NormalizeHeader(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace("ё", "е");
    }
}
