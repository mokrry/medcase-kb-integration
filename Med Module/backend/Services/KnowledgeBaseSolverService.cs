using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Services.Interfaces;

namespace MedicalFeaturePrototype.Api.Services;

public class KnowledgeBaseSolverService : IKnowledgeBaseSolverService
{
    private const string SolverUrl = "http://kb.ai-hippocrates.ru:8887/ai-hippocrates-solver/solver/key-value-request/1";
    private const string ActivationConditionSheetName = "Скрипт activation_condition";
    private const string NodeActivationConditionSheetName = "Скрипт node_activation_cond";
    private const string NodesSheetName = "Перечень узлов";

    private readonly IWebHostEnvironment _environment;
    private readonly HttpClient _httpClient;
    private readonly ILogger<KnowledgeBaseSolverService> _logger;

    public KnowledgeBaseSolverService(
        IWebHostEnvironment environment,
        HttpClient httpClient,
        ILogger<KnowledgeBaseSolverService> logger)
    {
        _environment = environment;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<KnowledgeBaseSolverDto> BuildAndSolveAsync(
        IReadOnlyList<string> finalSymptoms,
        string preparedComplaintsText,
        CancellationToken cancellationToken = default)
    {
        var result = BuildPayload(finalSymptoms, preparedComplaintsText);
        result.RequestJson = JsonSerializer.Serialize(result.Payload, new JsonSerializerOptions { WriteIndented = true });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SolverUrl)
            {
                Content = new StringContent(result.RequestJson, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            result.StatusCode = (int)response.StatusCode;
            result.ResponseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            result.IsSuccess = response.IsSuccessStatusCode;

            if (!response.IsSuccessStatusCode)
            {
                result.Error = $"Solver returned HTTP {(int)response.StatusCode}.";
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "[SERVER] Knowledge base solver request failed");
            result.Error = exception.Message;
            result.IsSuccess = false;
        }

        return result;
    }

    private KnowledgeBaseSolverDto BuildPayload(IReadOnlyList<string> finalSymptoms, string preparedComplaintsText)
    {
        var result = new KnowledgeBaseSolverDto();
        var knowledgeBasePath = ResolveKnowledgeBasePath();
        if (knowledgeBasePath is null)
        {
            result.Warnings.Add("Файл База_знаний_v24.5.xlsx не найден в backend/Data.");
            return result;
        }

        using var workbook = new XLWorkbook(knowledgeBasePath);
        var nodes = ReadNodes(workbook);
        var activationConditions = ReadActivationConditions(workbook);
        var linksByNodeId = ReadNodeActivationConditions(workbook)
            .GroupBy(item => item.NodeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var symptom in finalSymptoms.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            var normalizedSymptom = Normalize(symptom);
            if (!nodes.TryGetValue(normalizedSymptom, out var node))
            {
                result.Warnings.Add($"Не найден узел БЗ для симптома: {symptom}.");
                continue;
            }

            if (!linksByNodeId.TryGetValue(node.Id, out var links))
            {
                result.Warnings.Add($"Для узла БЗ не найдено правило активации: {symptom}.");
                continue;
            }

            foreach (var link in links)
            {
                if (!activationConditions.TryGetValue(link.ActivationConditionId, out var condition))
                {
                    result.Warnings.Add($"Не найден activation_condition #{link.ActivationConditionId} для симптома: {symptom}.");
                    continue;
                }

                var value = ResolveValue(condition, link, preparedComplaintsText, result.Warnings);
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.Warnings.Add($"Не удалось получить значение для label {condition.Label} по симптому: {symptom}.");
                    continue;
                }

                AddPayloadValue(result, condition.Label, value, symptom, node.Id, condition.Id, "finalSymptoms");
            }
        }

        AddTextDerivedValues(result, preparedComplaintsText);
        return result;
    }

    private static void AddPayloadValue(
        KnowledgeBaseSolverDto result,
        string label,
        string value,
        string symptom,
        int nodeId,
        int activationConditionId,
        string source)
    {
        if (result.Payload.TryGetValue(label, out var existingValue) && existingValue != value)
        {
            result.Warnings.Add($"Конфликт значений для label {label}: {existingValue} -> {value}. Использовано последнее значение.");
        }

        result.Payload[label] = value;
        result.Mappings.Add(new KnowledgeBaseMappingDto
        {
            Source = source,
            Symptom = symptom,
            NodeId = nodeId.ToString(CultureInfo.InvariantCulture),
            ActivationConditionId = activationConditionId.ToString(CultureInfo.InvariantCulture),
            Label = label,
            Value = value
        });
    }

    private static void AddTextDerivedValues(KnowledgeBaseSolverDto result, string preparedComplaintsText)
    {
        var normalizedText = Normalize(preparedComplaintsText);
        var temperature = ExtractTemperature(preparedComplaintsText);
        if (temperature is not null)
        {
            AddPayloadValue(result, "temperature", temperature.Value.ToString(CultureInfo.InvariantCulture), "Температура тела", 0, 9, "text");
        }

        if (!result.Payload.ContainsKey("dyspnea") && Regex.IsMatch(normalizedText, @"(?:одышк[аи]?|одышк)\s+(?:нет|отсутствует)|без\s+одышк"))
        {
            AddPayloadValue(result, "dyspnea", "0", "Одышка отсутствует", 0, 24, "text");
        }
    }

    private static string? ResolveValue(
        ActivationCondition condition,
        NodeActivationCondition link,
        string preparedComplaintsText,
        List<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(link.StringValue))
        {
            return link.StringValue;
        }

        if (!condition.IsNumeric)
        {
            return null;
        }

        if (condition.Label.Equals("temperature", StringComparison.OrdinalIgnoreCase))
        {
            var temperature = ExtractTemperature(preparedComplaintsText);
            if (temperature is not null)
            {
                return temperature.Value.ToString(CultureInfo.InvariantCulture);
            }

            if (!string.IsNullOrWhiteSpace(link.RangeStart))
            {
                warnings.Add("Температурный симптом найден без точного числа в тексте. Для solver использована нижняя граница диапазона БЗ.");
                return NormalizeDecimal(link.RangeStart);
            }
        }

        return null;
    }

    private string? ResolveKnowledgeBasePath()
    {
        var dataDirectory = Path.Combine(_environment.ContentRootPath, "Data");
        if (!Directory.Exists(dataDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(dataDirectory, "*.xlsx")
            .FirstOrDefault(path => Path.GetFileName(path).Contains("База_знаний_v24.5", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, KnowledgeBaseNode> ReadNodes(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheet(NodesSheetName);
        var nodes = new Dictionary<string, KnowledgeBaseNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            if (!TryGetInt(row.Cell(1).GetString(), out var id))
            {
                continue;
            }

            var name = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            nodes[Normalize(name)] = new KnowledgeBaseNode(id, name);
        }

        return nodes;
    }

    private static Dictionary<int, ActivationCondition> ReadActivationConditions(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheet(ActivationConditionSheetName);
        var conditions = new Dictionary<int, ActivationCondition>();

        foreach (var row in worksheet.RowsUsed().Skip(5))
        {
            if (!TryGetInt(row.Cell(2).GetString(), out var id))
            {
                continue;
            }

            var label = row.Cell(6).GetString().Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            conditions[id] = new ActivationCondition(
                id,
                row.Cell(4).GetString().Trim(),
                label,
                row.Cell(10).GetString().Equals("true", StringComparison.OrdinalIgnoreCase));
        }

        return conditions;
    }

    private static List<NodeActivationCondition> ReadNodeActivationConditions(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheet(NodeActivationConditionSheetName);
        var links = new List<NodeActivationCondition>();

        foreach (var row in worksheet.RowsUsed().Skip(6))
        {
            if (!TryGetInt(row.Cell(3).GetString(), out _)
                || !TryGetInt(row.Cell(5).GetString(), out var nodeId)
                || !TryGetInt(row.Cell(7).GetString(), out var activationConditionId))
            {
                continue;
            }

            links.Add(new NodeActivationCondition(
                nodeId,
                activationConditionId,
                NormalizeNullable(row.Cell(9).GetString()),
                NormalizeNullable(row.Cell(13).GetString()),
                NormalizeNullable(row.Cell(17).GetString())));
        }

        return links;
    }

    private static bool TryGetInt(string value, out int result)
    {
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static string? NormalizeNullable(string value)
    {
        var normalized = value.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Equals("null", StringComparison.OrdinalIgnoreCase)
            ? null
            : NormalizeDecimal(normalized);
    }

    private static string NormalizeDecimal(string value)
    {
        return value.Trim().Replace(',', '.');
    }

    private static string Normalize(string value)
    {
        var normalized = value.ToLowerInvariant().Replace('ё', 'е').Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized;
    }

    private static decimal? ExtractTemperature(string text)
    {
        var matches = Regex.Matches(text, @"(?<!\d)(?:3[7-9]|4[0-6])(?:[.,]\d)?(?!\d)");
        var values = new List<decimal>();

        foreach (Match match in matches)
        {
            if (decimal.TryParse(
                    match.Value.Replace(',', '.'),
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                values.Add(value);
            }
        }

        return values.Count == 0 ? null : values.Max();
    }

    private sealed record KnowledgeBaseNode(int Id, string Name);
    private sealed record ActivationCondition(int Id, string Name, string Label, bool IsNumeric);
    private sealed record NodeActivationCondition(int NodeId, int ActivationConditionId, string? RangeStart, string? RangeEnd, string? StringValue);
}
