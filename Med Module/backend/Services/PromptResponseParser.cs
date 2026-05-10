using System.Text.Json;

namespace MedicalFeaturePrototype.Api.Services;

internal static class PromptResponseParser
{
    public static IReadOnlyList<string> ParseLines(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        if (TryParseSymptomsJson(content, out var jsonSymptoms))
        {
            return jsonSymptoms;
        }

        return content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeOutputLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    public static string ReorderContentByWhitelist(string content, IReadOnlyList<string> whitelist)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return SerializeSymptoms([]);
        }

        var whitelistOrder = whitelist
            .Select((name, index) => new { Name = name.Trim(), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);

        var matched = new List<(int Index, string Name)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var normalizedLine in ParseLines(content))
        {
            if (!whitelistOrder.TryGetValue(normalizedLine, out var index))
            {
                continue;
            }

            var canonicalName = whitelist[index];
            if (!seen.Add(canonicalName))
            {
                continue;
            }

            matched.Add((index, canonicalName));
        }

        if (matched.Count == 0)
        {
            if (TryParseSymptomsJson(content, out var parsedSymptoms))
            {
                return SerializeSymptoms(parsedSymptoms);
            }

            return SerializeSymptoms([]);
        }

        return SerializeSymptoms(matched
            .OrderBy(item => item.Index)
            .Select(item => item.Name));
    }

    public static string NormalizeOutputLine(string line)
    {
        var normalized = line.Trim();

        if (normalized.StartsWith("вЂў", StringComparison.Ordinal) ||
            normalized.StartsWith("-", StringComparison.Ordinal) ||
            normalized.StartsWith("*", StringComparison.Ordinal))
        {
            normalized = normalized[1..].Trim();
        }

        var dotIndex = normalized.IndexOf(". ", StringComparison.Ordinal);
        if (dotIndex > 0 && normalized[..dotIndex].All(char.IsDigit))
        {
            normalized = normalized[(dotIndex + 2)..].Trim();
        }

        return normalized.TrimEnd('.', ';', ':').Trim();
    }

    private static bool TryParseSymptomsJson(string content, out string[] symptoms)
    {
        symptoms = [];
        var normalizedContent = ExtractJsonPayload(content);

        try
        {
            using var document = JsonDocument.Parse(normalizedContent);
            if (!document.RootElement.TryGetProperty("symptoms", out var symptomsElement) ||
                symptomsElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            symptoms = symptomsElement
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => NormalizeOutputLine(item.GetString() ?? string.Empty))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string ExtractJsonPayload(string content)
    {
        var trimmed = content.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = trimmed.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                trimmed = trimmed[(firstLineEnd + 1)..].Trim();
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3].Trim();
            }
        }

        var jsonStart = trimmed.IndexOf('{');
        var jsonEnd = trimmed.LastIndexOf('}');

        if (jsonStart >= 0 && jsonEnd >= jsonStart)
        {
            return trimmed.Substring(jsonStart, jsonEnd - jsonStart + 1);
        }

        return trimmed;
    }

    private static string SerializeSymptoms(IEnumerable<string> symptoms)
    {
        return JsonSerializer.Serialize(new
        {
            symptoms = symptoms
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray()
        });
    }
}
