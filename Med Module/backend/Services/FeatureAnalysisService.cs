using System.Text.RegularExpressions;
using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Services.Interfaces;

namespace MedicalFeaturePrototype.Api.Services;

public class FeatureAnalysisService : IFeatureAnalysisService
{
    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Продуктивный кашель"] = new[] { "кашель с мокротой", "кашель с мокрот", "продуктивный кашель" },
        ["Надсадный кашель"] = new[] { "надсадный кашель" },
        ["Одышка при физической активности"] = new[]
        {
            "одышка при физической нагрузке",
            "одышка при небольшой физической нагрузке",
            "одышка при нагрузке"
        },
        ["Одышка в покое"] = new[] { "одышка в покое" },
        ["Фебрильная температура"] = new[] { "температура 39", "температура 38", "лихорадка" },
        ["Субфебрильная температура"] = new[] { "37,0", "37,1", "37,2", "37,3", "37,4", "37,5", "37,6", "37,7" },
        ["Снижение сатурации"] = new[] { "spo2", "сатурац" },
        ["Курение в анамнезе"] = new[] { "кур", "вредные привычки" },
        ["ВИЧ-инфекция в анамнезе"] = new[] { "вич" },
        ["Сахарный диабет в анамнезе"] = new[] { "сахарный диабет", "диабет" },
        ["ХОБЛ в анамнезе"] = new[] { "хобл" },
        ["Бронхиальная астма в анамнезе"] = new[] { "бронхиальная астма", "астма" },
        ["Тахикардия"] = new[] { "тахикард", "чсс" }
    };

    public IReadOnlyList<FeatureCheckResult> Analyze(string fullText, IReadOnlyList<TargetFeature> features)
    {
        var normalizedText = Normalize(fullText);
        var results = new List<FeatureCheckResult>();

        foreach (var feature in features)
        {
            var patterns = BuildPatterns(feature.Name);
            var foundEvidence = FindEvidence(fullText, normalizedText, patterns);

            if (foundEvidence is not null)
            {
                results.Add(new FeatureCheckResult
                {
                    FeatureName = feature.Name,
                    Category = feature.Category,
                    Status = "Found",
                    Evidence = foundEvidence
                });
                continue;
            }

            var specialCase = TryHandleSpecialCase(feature.Name, fullText, normalizedText);
            if (specialCase is not null)
            {
                results.Add(new FeatureCheckResult
                {
                    FeatureName = feature.Name,
                    Category = feature.Category,
                    Status = specialCase.Value.status,
                    Evidence = specialCase.Value.evidence
                });
                continue;
            }

            results.Add(new FeatureCheckResult
            {
                FeatureName = feature.Name,
                Category = feature.Category,
                Status = "NotFound",
                Evidence = string.Empty
            });
        }

        return results;
    }

    private static (string status, string evidence)? TryHandleSpecialCase(string featureName, string originalText, string normalizedText)
    {
        if (featureName.Contains("сатурац", StringComparison.OrdinalIgnoreCase))
        {
            var match = Regex.Match(originalText, @"SpO2\s*(\d{2,3})%?", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var spo2))
            {
                return spo2 < 95
                    ? ("Found", $"Обнаружено значение сатурации: SpO2 {spo2}%")
                    : ("NeedsReview", $"Сатурация указана как SpO2 {spo2}%, требуется интерпретация порога.");
            }
        }

        if (featureName.Contains("Курение", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedText.Contains("вредные привычки: отрицает"))
            {
                return ("NotFound", "В тексте указано: 'Вредные привычки: отрицает'.");
            }
        }

        if (featureName.Contains("ВИЧ", StringComparison.OrdinalIgnoreCase))
        {
            if (normalizedText.Contains("вич: отрицает") || normalizedText.Contains("вич отрицает"))
            {
                return ("NotFound", "В тексте указано отрицание ВИЧ в анамнезе.");
            }
        }

        return null;
    }

    private static string[] BuildPatterns(string featureName)
    {
        if (Synonyms.TryGetValue(featureName, out var mapped))
        {
            return mapped;
        }

        return new[] { featureName };
    }

    private static string? FindEvidence(string originalText, string normalizedText, IEnumerable<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            var normalizedPattern = Normalize(pattern);
            var index = normalizedText.IndexOf(normalizedPattern, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                continue;
            }

            var safeIndex = Math.Clamp(index, 0, Math.Max(0, originalText.Length - 1));
            var start = Math.Max(0, safeIndex - 40);
            var length = Math.Min(originalText.Length - start, normalizedPattern.Length + 80);
            return originalText.Substring(start, length).Trim();
        }

        return null;
    }

    private static string Normalize(string value)
    {
        var normalized = value.ToLowerInvariant().Replace("ё", "е");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.Trim();
    }
}
