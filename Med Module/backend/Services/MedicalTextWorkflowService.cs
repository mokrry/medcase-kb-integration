using System.Text;
using System.Text.Json;
using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Services.Interfaces;

namespace MedicalFeaturePrototype.Api.Services;

public class MedicalTextWorkflowService : IMedicalTextWorkflowService
{
    private readonly IGeminiApiService _geminiApiService;
    private readonly ILogger<MedicalTextWorkflowService> _logger;

    public MedicalTextWorkflowService(
        IGeminiApiService geminiApiService,
        ILogger<MedicalTextWorkflowService> logger)
    {
        _geminiApiService = geminiApiService;
        _logger = logger;
    }

    public async Task<MedicalTextPreparationResult> PrepareMedicalTextAsync(
        string complaintsText,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPreparationPrompt(complaintsText);
        var response = await _geminiApiService.GenerateAsync(
            new LlmPromptRequest("gemini", requestId, prompt, complaintsText, []),
            cancellationToken);

        var preparedText = response.Content.Trim();
        _logger.LogInformation(
            "[SERVER] Gemini medical text preparation finished RequestId={RequestId} PreparedLength={PreparedLength}",
            requestId,
            preparedText.Length);

        return new MedicalTextPreparationResult(
            response.Provider,
            response.Model,
            prompt,
            string.IsNullOrWhiteSpace(preparedText) ? complaintsText.Trim() : preparedText,
            response.RawResponse);
    }

    public async Task<DisputedSymptomsResolutionResult> ResolveDisputedSymptomsAsync(
        string preparedComplaintsText,
        IReadOnlyList<string> matchedSymptoms,
        IReadOnlyList<string> disputedSymptoms,
        IReadOnlyList<string> whitelist,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        if (disputedSymptoms.Count == 0)
        {
            return new DisputedSymptomsResolutionResult(
                "gemini",
                string.Empty,
                string.Empty,
                """{"symptoms":[]}""",
                string.Empty,
                [],
                []);
        }

        var prompt = BuildDisputeResolutionPrompt(preparedComplaintsText, matchedSymptoms, disputedSymptoms);
        var response = await _geminiApiService.GenerateAsync(
            new LlmPromptRequest("gemini", requestId, prompt, preparedComplaintsText, disputedSymptoms),
            cancellationToken);

        var normalizedContent = PromptResponseParser.ReorderContentByWhitelist(response.Content, whitelist);
        var confirmedSymptoms = PromptResponseParser.ParseLines(normalizedContent)
            .Where(symptom => disputedSymptoms.Contains(symptom, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _logger.LogInformation(
            "[SERVER] Gemini disputed symptoms resolution finished RequestId={RequestId} Candidates={CandidatesCount} Confirmed={ConfirmedCount}",
            requestId,
            disputedSymptoms.Count,
            confirmedSymptoms.Length);

        return new DisputedSymptomsResolutionResult(
            response.Provider,
            response.Model,
            prompt,
            normalizedContent,
            response.RawResponse,
            disputedSymptoms.ToArray(),
            confirmedSymptoms);
    }

    public async Task<SymptomEvidenceVerificationDto> VerifySymptomEvidenceAsync(
        string sourceComplaintsText,
        string preparedComplaintsText,
        IReadOnlyList<string> finalSymptoms,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        if (finalSymptoms.Count == 0)
        {
            return new SymptomEvidenceVerificationDto
            {
                Stage = new PromptWorkflowStageDto
                {
                    Provider = "gemini",
                    Content = """{"symptoms":[]}"""
                }
            };
        }

        var sourceText = string.IsNullOrWhiteSpace(sourceComplaintsText)
            ? preparedComplaintsText
            : sourceComplaintsText.Trim();
        var prompt = BuildEvidenceVerificationPrompt(sourceText, finalSymptoms);
        var response = await _geminiApiService.GenerateAsync(
            new LlmPromptRequest("gemini", requestId, prompt, sourceText, finalSymptoms),
            cancellationToken);

        var evidences = ParseEvidenceResponse(response.Content, finalSymptoms)
            .Select(item => VerifyEvidenceLocation(item, sourceText))
            .ToList();

        foreach (var symptom in finalSymptoms)
        {
            if (evidences.Any(item => item.Name.Equals(symptom, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            evidences.Add(new SymptomEvidenceDto
            {
                Name = symptom,
                VerificationStatus = "needsReview"
            });
        }

        return new SymptomEvidenceVerificationDto
        {
            Symptoms = evidences,
            Stage = new PromptWorkflowStageDto
            {
                Provider = response.Provider,
                Model = response.Model,
                Prompt = prompt,
                Content = SerializeEvidence(evidences),
                RawResponse = response.RawResponse
            }
        };
    }

    private static string BuildPreparationPrompt(string complaintsText)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Ты — врач-редактор медицинского текста.");
        builder.AppendLine("Твоя задача — привести жалобы пациента к аккуратному и понятному медицинскому виду.");
        builder.AppendLine("Нужно:");
        builder.AppendLine("1. исправить очевидные опечатки;");
        builder.AppendLine("2. исправить очевидное смешение кириллицы и латиницы;");
        builder.AppendLine("3. расшифровать очевидные медицинские сокращения;");
        builder.AppendLine("4. сохранить весь медицинский смысл без потерь;");
        builder.AppendLine("5. не удалять симптомы, характеристики, отрицания, исторические замечания и временные указания;");
        builder.AppendLine("6. не добавлять новую медицинскую информацию от себя;");
        builder.AppendLine("7. не ставить диагноз и не делать выводы.");
        builder.AppendLine();
        builder.AppendLine("Верни только итоговый исправленный медицинский текст без комментариев, без пояснений и без markdown.");
        builder.AppendLine();
        builder.AppendLine("Исходный текст:");
        builder.AppendLine(complaintsText);
        return builder.ToString().Trim();
    }

    private static string BuildDisputeResolutionPrompt(
        string preparedComplaintsText,
        IReadOnlyList<string> matchedSymptoms,
        IReadOnlyList<string> disputedSymptoms)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Ты — врач-эксперт по верификации симптомов в медицинском тексте.");
        builder.AppendLine("Нужно проверить только перечисленные спорные симптомы.");
        builder.AppendLine("Ниже также приведены симптомы, которые уже совпали между моделями. Используй их как контекст, чтобы исключать сомнительные варианты.");
        builder.AppendLine("Для каждого симптома оцени, соответствует ли он тому, что написано в медицинском тексте.");
        builder.AppendLine("Думай как врач и используй клиническую интерпретацию, но только в пределах разумного соответствия симптому.");
        builder.AppendLine("Разрешено подтверждать симптом не только при буквальном совпадении слов, но и при клинически эквивалентном описании в тексте.");
        builder.AppendLine("Нельзя ставить диагноз, додумывать отсутствующие признаки или делать слишком далекие клинические выводы.");
        builder.AppendLine("Нельзя подтверждать спорный симптом, если он конфликтует с уже совпавшими симптомами или является менее точным вариантом по сравнению с уже совпавшим более точным симптомом.");
        builder.AppendLine("Если уже совпавший симптом точнее и полностью покрывает спорный вариант, спорный симптом не включай.");
        builder.AppendLine("Нельзя добавлять новые симптомы.");
        builder.AppendLine("Нельзя возвращать симптомы вне списка кандидатов.");
        builder.AppendLine("Симптомы из списка совпавших не нужно возвращать в ответе: они приведены только как контекст.");
        builder.AppendLine("Если симптом не соответствует описанию в тексте, не включай его.");
        builder.AppendLine("Примеры допустимой клинической интерпретации:");
        builder.AppendLine("1. Если в тексте есть кашель и есть мокрота любого типа, это соответствует симптому «Продуктивный кашель».");
        builder.AppendLine("2. Если в тексте есть «кашель со слизисто-гнойной мокротой», это соответствует симптому «Продуктивный кашель».");
        builder.AppendLine("3. Если в тексте есть одышка при нагрузке, при ходьбе, при самообслуживании или при физической активности, это может соответствовать симптому «Одышка при физической активности».");
        builder.AppendLine("4. Если указана числовая температура, симптом температуры нужно соотносить с подходящей температурной категорией.");
        builder.AppendLine("5. Если уже совпал более точный симптом, не подтверждай конфликтующий или более общий спорный симптом только потому, что он частично подходит по тексту.");
        builder.AppendLine("Верни только JSON формата {\"symptoms\":[...]}, где внутри только симптомы из списка кандидатов, которые соответствуют медицинскому тексту.");
        builder.AppendLine();
        builder.AppendLine("Медицинский текст:");
        builder.AppendLine(preparedComplaintsText);
        builder.AppendLine();
        builder.AppendLine("Уже совпавшие симптомы:");

        if (matchedSymptoms.Count == 0)
        {
            builder.AppendLine("Список пуст.");
        }
        else
        {
            foreach (var symptom in matchedSymptoms)
            {
                builder.Append("- ").AppendLine(symptom);
            }
        }

        builder.AppendLine();
        builder.AppendLine("Спорные симптомы:");

        foreach (var symptom in disputedSymptoms)
        {
            builder.Append("- ").AppendLine(symptom);
        }

        return builder.ToString().Trim();
    }

    private static string BuildEvidenceVerificationPrompt(
        string sourceComplaintsText,
        IReadOnlyList<string> finalSymptoms)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Ты — врач-эксперт по поиску текстовых подтверждений симптомов.");
        builder.AppendLine("Нужно проверить только перечисленные финальные симптомы.");
        builder.AppendLine("Статус «Проверено» возможен только если в исходном тексте есть прямая лексическая форма самого симптома.");
        builder.AppendLine("Перефразы, клинически близкие описания и смысловые соответствия не считаются прямой проверкой.");
        builder.AppendLine("Для evidence верни короткий дословный фрагмент исходного текста.");
        builder.AppendLine("Если есть прямая форма симптома, верни именно ее цитату.");
        builder.AppendLine("Если прямой формы нет, но есть фрагмент, который может косвенно относиться к симптому, верни этот вероятный фрагмент как evidence.");
        builder.AppendLine("Если нет даже вероятного фрагмента, верни пустую строку evidence.");
        builder.AppendLine("Пример: для симптома «Кровохарканье» фраза «с прожилками крови» не является прямой формой симптома; ее можно вернуть как вероятный фрагмент, но такой симптом требует ручной проверки.");
        builder.AppendLine("Пример: для симптома «Слизисто-гнойная мокрота» фраза «гнойная мокрота» не является прямой формой симптома; ее можно вернуть как вероятный фрагмент, но такой симптом требует ручной проверки.");
        builder.AppendLine("Нельзя добавлять новые симптомы.");
        builder.AppendLine("Нельзя возвращать симптомы вне списка.");
        builder.AppendLine("Верни только JSON формата {\"symptoms\":[{\"name\":\"...\",\"evidence\":\"...\"}]}.");
        builder.AppendLine();
        builder.AppendLine("Исходный медицинский текст:");
        builder.AppendLine(sourceComplaintsText);
        builder.AppendLine();
        builder.AppendLine("Финальные симптомы:");

        foreach (var symptom in finalSymptoms)
        {
            builder.Append("- ").AppendLine(symptom);
        }

        return builder.ToString().Trim();
    }

    private static List<SymptomEvidenceDto> ParseEvidenceResponse(string content, IReadOnlyList<string> finalSymptoms)
    {
        var allowedSymptoms = finalSymptoms.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new List<SymptomEvidenceDto>();

        try
        {
            using var document = JsonDocument.Parse(PromptResponseParser.ExtractJsonPayload(content));
            if (!document.RootElement.TryGetProperty("symptoms", out var symptomsElement) ||
                symptomsElement.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in symptomsElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = item.TryGetProperty("name", out var nameElement)
                    ? PromptResponseParser.NormalizeOutputLine(nameElement.GetString() ?? string.Empty)
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(name) || !allowedSymptoms.Contains(name))
                {
                    continue;
                }

                var evidence = item.TryGetProperty("evidence", out var evidenceElement)
                    ? evidenceElement.GetString()?.Trim() ?? string.Empty
                    : string.Empty;

                result.Add(new SymptomEvidenceDto
                {
                    Name = finalSymptoms.First(symptom => symptom.Equals(name, StringComparison.OrdinalIgnoreCase)),
                    Evidence = evidence
                });
            }
        }
        catch (JsonException)
        {
            return result;
        }

        return result
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static SymptomEvidenceDto VerifyEvidenceLocation(SymptomEvidenceDto item, string sourceComplaintsText)
    {
        if (string.IsNullOrWhiteSpace(item.Evidence))
        {
            ApplyLikelyEvidenceFallback(item, sourceComplaintsText);
            item.VerificationStatus = "needsReview";
            return item;
        }

        var index = sourceComplaintsText.IndexOf(item.Evidence, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            ApplyLikelyEvidenceFallback(item, sourceComplaintsText);
            item.VerificationStatus = "needsReview";
            return item;
        }

        item.EvidenceStart = index;
        item.EvidenceEnd = index + item.Evidence.Length;
        item.Evidence = sourceComplaintsText.Substring(index, item.Evidence.Length);
        item.VerificationStatus = IsDirectLexicalEvidence(item.Name, item.Evidence)
            ? "verified"
            : "needsReview";
        return item;
    }

    private static void ApplyLikelyEvidenceFallback(SymptomEvidenceDto item, string preparedComplaintsText)
    {
        var fallback = FindLikelyEvidence(item.Name, preparedComplaintsText);
        if (fallback is null)
        {
            return;
        }

        item.EvidenceStart = fallback.Value.Start;
        item.EvidenceEnd = fallback.Value.End;
        item.Evidence = preparedComplaintsText.Substring(fallback.Value.Start, fallback.Value.End - fallback.Value.Start);
    }

    private static bool IsDirectLexicalEvidence(string symptom, string evidence)
    {
        var normalizedSymptom = NormalizeEvidenceText(symptom);
        var normalizedEvidence = NormalizeEvidenceText(evidence);

        if (string.IsNullOrWhiteSpace(normalizedSymptom) || string.IsNullOrWhiteSpace(normalizedEvidence))
        {
            return false;
        }

        if (normalizedEvidence.Contains(normalizedSymptom, StringComparison.Ordinal))
        {
            return true;
        }

        return normalizedSymptom switch
        {
            var value when value.Contains("кровохаркан", StringComparison.Ordinal) =>
                normalizedEvidence.Contains("кровохаркан", StringComparison.Ordinal),

            var value when value.Contains("мокрота с примесью крови", StringComparison.Ordinal) =>
                normalizedEvidence.Contains("мокрот", StringComparison.Ordinal)
                && normalizedEvidence.Contains("примес", StringComparison.Ordinal)
                && normalizedEvidence.Contains("кров", StringComparison.Ordinal),

            var value when value.Contains("слизисто-гнойная мокрота", StringComparison.Ordinal) =>
                normalizedEvidence.Contains("слизисто", StringComparison.Ordinal)
                && normalizedEvidence.Contains("гной", StringComparison.Ordinal)
                && normalizedEvidence.Contains("мокрот", StringComparison.Ordinal),

            var value when value.Contains("гнойная мокрота", StringComparison.Ordinal) =>
                normalizedEvidence.Contains("гной", StringComparison.Ordinal)
                && normalizedEvidence.Contains("мокрот", StringComparison.Ordinal),

            var value when value.Contains("боль в грудной клетке", StringComparison.Ordinal) =>
                normalizedEvidence.Contains("бол", StringComparison.Ordinal)
                && normalizedEvidence.Contains("груд", StringComparison.Ordinal)
                && normalizedEvidence.Contains("клет", StringComparison.Ordinal),

            var value when value.Contains("пиретическая температура", StringComparison.Ordinal) =>
                normalizedEvidence.Contains("пиретичес", StringComparison.Ordinal),

            var value when value.Contains("фебрильная температура", StringComparison.Ordinal) =>
                normalizedEvidence.Contains("фебриль", StringComparison.Ordinal),

            var value when value.Contains("субфебрильная температура", StringComparison.Ordinal) =>
                normalizedEvidence.Contains("субфебриль", StringComparison.Ordinal),

            var value when value.Contains("гиперпиретическая температура", StringComparison.Ordinal) =>
                normalizedEvidence.Contains("гиперпирет", StringComparison.Ordinal),

            var value when value.Contains("продуктивный кашель", StringComparison.Ordinal) =>
                normalizedEvidence.Contains("продуктив", StringComparison.Ordinal)
                && normalizedEvidence.Contains("каш", StringComparison.Ordinal),

            _ => false
        };
    }

    private static string NormalizeEvidenceText(string value)
    {
        var normalized = value.ToLowerInvariant().Replace('ё', 'е');
        normalized = normalized
            .Replace('«', ' ')
            .Replace('»', ' ')
            .Replace('"', ' ');
        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static (int Start, int End)? FindLikelyEvidence(string symptom, string preparedComplaintsText)
    {
        var normalizedSymptom = symptom.ToLowerInvariant().Replace('ё', 'е');
        var sentenceRanges = SplitIntoSentenceRanges(preparedComplaintsText);

        if (normalizedSymptom.Contains("мокрота", StringComparison.Ordinal))
        {
            var range = FindSentenceRange(sentenceRanges, preparedComplaintsText, ["мокрот"]);
            if (range is not null)
            {
                return range;
            }
        }

        if (normalizedSymptom.Contains("кашель", StringComparison.Ordinal))
        {
            var range = FindSentenceRange(sentenceRanges, preparedComplaintsText, ["каш"]);
            if (range is not null)
            {
                return range;
            }
        }

        if (normalizedSymptom.Contains("одыш", StringComparison.Ordinal))
        {
            var range = FindSentenceRange(sentenceRanges, preparedComplaintsText, ["одыш"]);
            if (range is not null)
            {
                return range;
            }
        }

        if (normalizedSymptom.Contains("температур", StringComparison.Ordinal))
        {
            var range = FindSentenceRange(sentenceRanges, preparedComplaintsText, ["температур", "лихорад"]);
            if (range is not null)
            {
                return range;
            }
        }

        if (normalizedSymptom.Contains("боль", StringComparison.Ordinal))
        {
            var range = FindSentenceRange(sentenceRanges, preparedComplaintsText, ["бол"]);
            if (range is not null)
            {
                return range;
            }
        }

        return null;
    }

    private static (int Start, int End)? FindSentenceRange(
        IReadOnlyList<(int Start, int End)> sentenceRanges,
        string text,
        IReadOnlyList<string> markers)
    {
        foreach (var range in sentenceRanges)
        {
            var sentence = text.Substring(range.Start, range.End - range.Start).ToLowerInvariant().Replace('ё', 'е');
            if (markers.Any(marker => sentence.Contains(marker, StringComparison.Ordinal)))
            {
                return range;
            }
        }

        return null;
    }

    private static List<(int Start, int End)> SplitIntoSentenceRanges(string text)
    {
        var ranges = new List<(int Start, int End)>();
        var start = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('.' or '!' or '?' or '\n'))
            {
                continue;
            }

            AddTrimmedRange(ranges, text, start, index + 1);
            start = index + 1;
        }

        AddTrimmedRange(ranges, text, start, text.Length);
        return ranges;
    }

    private static void AddTrimmedRange(List<(int Start, int End)> ranges, string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        if (end > start)
        {
            ranges.Add((start, end));
        }
    }

    private static string SerializeEvidence(IEnumerable<SymptomEvidenceDto> evidences)
    {
        return JsonSerializer.Serialize(new
        {
            symptoms = evidences.Select(item => new
            {
                name = item.Name,
                evidence = item.Evidence,
                evidenceStart = item.EvidenceStart,
                evidenceEnd = item.EvidenceEnd,
                verificationStatus = item.VerificationStatus
            })
        });
    }
}
