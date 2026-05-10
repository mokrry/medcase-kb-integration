using System.Globalization;
using System.Text.RegularExpressions;
using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Services.Interfaces;

namespace MedicalFeaturePrototype.Api.Services;

public class PromptVotingService : IPromptVotingService
{
    private static readonly string[] DefaultProviders = ["gemini", "chatgpt"];

    private readonly IPromptComposerService _promptComposerService;
    private readonly IMedicalTextWorkflowService _medicalTextWorkflowService;
    private readonly IChatGptApiService _chatGptApiService;
    private readonly IGeminiApiService _geminiApiService;
    private readonly IGigaChatApiService _gigaChatApiService;
    private readonly ILogger<PromptVotingService> _logger;

    public PromptVotingService(
        IPromptComposerService promptComposerService,
        IMedicalTextWorkflowService medicalTextWorkflowService,
        IChatGptApiService chatGptApiService,
        IGeminiApiService geminiApiService,
        IGigaChatApiService gigaChatApiService,
        ILogger<PromptVotingService> logger)
    {
        _promptComposerService = promptComposerService;
        _medicalTextWorkflowService = medicalTextWorkflowService;
        _chatGptApiService = chatGptApiService;
        _geminiApiService = geminiApiService;
        _gigaChatApiService = gigaChatApiService;
        _logger = logger;
    }

    public async Task<PromptRunBundleResponseDto> ExecuteBundleAsync(
        PromptRunRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var preparationRequestId = $"PREP-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        var preparationResult = await _medicalTextWorkflowService.PrepareMedicalTextAsync(
            request.ComplaintsText,
            preparationRequestId,
            cancellationToken);

        var executionPayload = await _promptComposerService.BuildExecutionPayloadAsync(
            request,
            preparationResult.Content,
            cancellationToken);

        var providers = ResolveProviders(request);
        var tasks = providers.Select(provider => ExecuteProviderAsync(provider, executionPayload.LlmRequest, cancellationToken));
        var results = (await Task.WhenAll(tasks))
            .OrderBy(result => providers.IndexOf(result.Provider))
            .ToList();

        var votingResult = await BuildVotingSummaryAsync(
            results,
            executionPayload.LlmRequest.Symptoms,
            executionPayload.LlmRequest.ComplaintsText,
            executionPayload.LlmRequest.RequestId,
            cancellationToken);

        var evidenceVerification = await _medicalTextWorkflowService.VerifySymptomEvidenceAsync(
            request.ComplaintsText,
            executionPayload.LlmRequest.ComplaintsText,
            votingResult.Voting.FinalSymptoms,
            $"EVIDENCE-{executionPayload.LlmRequest.RequestId}",
            cancellationToken);

        _logger.LogInformation(
            "[SERVER] Prompt voting finished RequestId={RequestId} SuccessfulProviders={SuccessfulProviders} DisputedSymptoms={DisputedSymptomsCount}",
            executionPayload.LlmRequest.RequestId,
            results.Count(result => string.IsNullOrWhiteSpace(result.Error)),
            votingResult.Voting.DisputedSymptoms.Count);

        return new PromptRunBundleResponseDto
        {
            PromptBuild = executionPayload.PromptBuild,
            Preparation = new PromptWorkflowStageDto
            {
                Provider = preparationResult.Provider,
                Model = preparationResult.Model,
                Prompt = preparationResult.Prompt,
                Content = preparationResult.Content,
                RawResponse = preparationResult.RawResponse
            },
            Results = results,
            DisputeResolution = votingResult.DisputeResolution,
            Voting = votingResult.Voting,
            EvidenceVerification = evidenceVerification
        };
    }

    private async Task<PromptProviderExecutionDto> ExecuteProviderAsync(
        string provider,
        LlmPromptRequest baseRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            var llmRequest = baseRequest with { Provider = provider };
            var llmResponse = provider switch
            {
                "chatgpt" => await _chatGptApiService.GenerateAsync(llmRequest, cancellationToken),
                "gemini" => await _geminiApiService.GenerateAsync(llmRequest, cancellationToken),
                "gigachat" => await _gigaChatApiService.GenerateAsync(llmRequest, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported LLM provider: {provider}")
            };

            var normalizedContent = PromptResponseParser.ReorderContentByWhitelist(llmResponse.Content, llmRequest.Symptoms);

            return new PromptProviderExecutionDto
            {
                Provider = llmResponse.Provider,
                Model = llmResponse.Model,
                Prompt = llmRequest.Prompt,
                Content = normalizedContent,
                RawResponse = llmResponse.RawResponse,
                Review = ReviewResponse(normalizedContent, llmRequest)
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "[SERVER] Provider execution failed RequestId={RequestId} Provider={Provider}",
                baseRequest.RequestId,
                provider);

            return new PromptProviderExecutionDto
            {
                Provider = provider,
                Error = exception.Message,
                Review = new PromptResponseReviewDto
                {
                    IsCompliant = false,
                    HasCriticalIssues = true,
                    Issues = ["Provider execution failed."]
                }
            };
        }
    }

    private static PromptResponseReviewDto ReviewResponse(string content, LlmPromptRequest request)
    {
        var rawLines = PromptResponseParser.ParseLines(content);
        var isJsonResponse = !string.IsNullOrWhiteSpace(content) && content.TrimStart().StartsWith("{", StringComparison.Ordinal);
        var whitelistSet = request.Symptoms
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var extractedSymptoms = new List<string>();
        var issues = new List<string>();
        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(content))
        {
            issues.Add("Model returned an empty response.");
        }

        if (rawLines.Count == 0 && !string.IsNullOrWhiteSpace(content) && !isJsonResponse)
        {
            issues.Add("Response could not be parsed as JSON symptoms payload.");
        }

        foreach (var line in rawLines)
        {
            if (!whitelistSet.Contains(line))
            {
                issues.Add($"Unknown or non-canonical symptom: {line}");
                continue;
            }

            if (!seen.Add(line))
            {
                issues.Add($"Duplicate symptom in response: {line}");
                continue;
            }

            extractedSymptoms.Add(line);

            switch (EvaluateSupport(line, request.ComplaintsText))
            {
                case SupportState.NotFound:
                    warnings.Add($"No direct support found in complaint text: {line}");
                    break;
                case SupportState.Contradicted:
                    issues.Add($"Symptom is contradicted by complaint text: {line}");
                    break;
            }
        }

        return BuildReviewResult(extractedSymptoms, issues, warnings);
    }

    private static PromptResponseReviewDto BuildReviewResult(
        List<string> extractedSymptoms,
        List<string> issues,
        List<string> warnings)
    {
        return new PromptResponseReviewDto
        {
            IsCompliant = issues.Count == 0,
            HasCriticalIssues = issues.Count > 0,
            Score = Math.Max(0, 100 - (issues.Count * 25) - (warnings.Count * 10)),
            ExtractedSymptoms = extractedSymptoms,
            Issues = issues,
            Warnings = warnings
        };
    }

    private async Task<(PromptVotingSummaryDto Voting, PromptDisputeResolutionDto DisputeResolution)> BuildVotingSummaryAsync(
        IReadOnlyCollection<PromptProviderExecutionDto> results,
        IReadOnlyList<string> whitelist,
        string preparedComplaintsText,
        string requestId,
        CancellationToken cancellationToken)
    {
        var successfulResults = results
            .Where(result => string.IsNullOrWhiteSpace(result.Error))
            .ToList();

        var majorityThreshold = successfulResults.Count == 0 ? 0 : (successfulResults.Count / 2) + 1;
        var whitelistOrder = whitelist
            .Select((symptom, index) => new { Symptom = symptom, Index = index })
            .ToDictionary(item => item.Symptom, item => item.Index, StringComparer.OrdinalIgnoreCase);
        var voteMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in successfulResults)
        {
            foreach (var symptom in result.Review.ExtractedSymptoms)
            {
                if (!voteMap.TryGetValue(symptom, out var providers))
                {
                    providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    voteMap[symptom] = providers;
                }

                providers.Add(result.Provider);
            }
        }

        var consensusSymptoms = voteMap
            .Where(item => item.Value.Count == successfulResults.Count && successfulResults.Count > 0)
            .Select(item => item.Key)
            .OrderBy(symptom => whitelistOrder.GetValueOrDefault(symptom, int.MaxValue))
            .ThenBy(symptom => symptom)
            .ToList();

        var disputedSymptoms = voteMap
            .Where(item => item.Value.Count < successfulResults.Count)
            .Select(item => item.Key)
            .OrderBy(symptom => whitelistOrder.GetValueOrDefault(symptom, int.MaxValue))
            .ThenBy(symptom => symptom)
            .ToList();

        var disputeResolutionResult = await _medicalTextWorkflowService.ResolveDisputedSymptomsAsync(
                preparedComplaintsText,
                consensusSymptoms,
                disputedSymptoms,
                whitelist,
                $"VERIFY-{requestId}",
                cancellationToken);

        var geminiConfirmedSymptoms = disputeResolutionResult.ConfirmedSymptoms
            .OrderBy(symptom => whitelistOrder.GetValueOrDefault(symptom, int.MaxValue))
            .ThenBy(symptom => symptom)
            .ToList();

        var finalSymptoms = consensusSymptoms
            .Concat(geminiConfirmedSymptoms)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(symptom => whitelistOrder.GetValueOrDefault(symptom, int.MaxValue))
            .ThenBy(symptom => symptom)
            .ToList();

        var geminiConfirmedSet = geminiConfirmedSymptoms.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var finalSymptomsSet = finalSymptoms.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var voteDetails = voteMap
            .Select(item => new PromptSymptomVoteDto
            {
                Symptom = item.Key,
                Votes = item.Value.Count,
                Providers = item.Value.OrderBy(value => value).ToList(),
                ReachedMajority = majorityThreshold > 0 && item.Value.Count >= majorityThreshold,
                ResolvedByGemini = geminiConfirmedSet.Contains(item.Key),
                IncludedInFinalAnswer = finalSymptomsSet.Contains(item.Key)
            })
            .OrderByDescending(item => item.Votes)
            .ThenBy(item => whitelistOrder.GetValueOrDefault(item.Symptom, int.MaxValue))
            .ThenBy(item => item.Symptom)
            .ToList();

        var voting = new PromptVotingSummaryDto
        {
            RequestedProviders = results.Count,
            SuccessfulProviders = successfulResults.Count,
            MajorityThreshold = majorityThreshold,
            PreparedComplaintsText = preparedComplaintsText,
            ConsensusSymptoms = consensusSymptoms,
            DisputedSymptoms = disputedSymptoms,
            GeminiConfirmedSymptoms = geminiConfirmedSymptoms,
            FinalSymptoms = finalSymptoms,
            VoteDetails = voteDetails
        };

        var disputeResolution = new PromptDisputeResolutionDto
        {
            CandidateSymptoms = disputeResolutionResult.CandidateSymptoms.ToList(),
            ConfirmedSymptoms = geminiConfirmedSymptoms,
            Stage = new PromptWorkflowStageDto
            {
                Provider = disputeResolutionResult.Provider,
                Model = disputeResolutionResult.Model,
                Prompt = disputeResolutionResult.Prompt,
                Content = disputeResolutionResult.Content,
                RawResponse = disputeResolutionResult.RawResponse
            }
        };

        return (voting, disputeResolution);
    }

    private static List<string> ResolveProviders(PromptRunRequestDto request)
    {
        if (request.Providers is { Length: > 0 })
        {
            return request.Providers
                .Select(PromptExecutionService.NormalizeProvider)
                .Where(provider => !string.IsNullOrWhiteSpace(provider))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var normalizedProvider = PromptExecutionService.NormalizeProvider(request.Provider);
        if (!string.IsNullOrWhiteSpace(normalizedProvider))
        {
            return [normalizedProvider];
        }

        return [.. DefaultProviders];
    }

    private static SupportState EvaluateSupport(string symptom, string complaintsText)
    {
        if (string.IsNullOrWhiteSpace(complaintsText))
        {
            return SupportState.NotFound;
        }

        var normalizedText = NormalizeSupportText(complaintsText);
        var normalizedSymptom = NormalizeSupportText(symptom);

        if (IsContradicted(normalizedText, normalizedSymptom))
        {
            return SupportState.Contradicted;
        }

        if (normalizedText.Contains(normalizedSymptom, StringComparison.Ordinal))
        {
            return SupportState.Found;
        }

        if (TryMatchTemperature(normalizedSymptom, normalizedText))
        {
            return SupportState.Found;
        }

        if (TryMatchProductiveCough(normalizedSymptom, normalizedText))
        {
            return SupportState.Found;
        }

        if (TryMatchNormalizedSupport(normalizedSymptom, normalizedText))
        {
            return SupportState.Found;
        }

        return SupportState.NotFound;
    }

    private static string NormalizeSupportText(string value)
    {
        var normalized = value.ToLowerInvariant().Replace('ё', 'е');
        normalized = normalized
            .Replace("в грудной клетки", "в грудной клетке")
            .Replace("боли в грудной клетки", "боли в грудной клетке")
            .Replace("боль в грудной клетки", "боль в грудной клетке")
            .Replace("в течении", "в течение");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private static bool IsContradicted(string text, string symptom)
    {
        var contradictionMarkers = new[]
        {
            $"нет {symptom}",
            $"не {symptom}",
            $"без {symptom}",
            $"отрицает {symptom}"
        };

        return contradictionMarkers.Any(text.Contains);
    }

    private static bool TryMatchProductiveCough(string symptom, string text)
    {
        return symptom.Contains("продуктивный кашель", StringComparison.Ordinal)
               && text.Contains("каш", StringComparison.Ordinal)
               && (text.Contains("мокрот", StringComparison.Ordinal) || text.Contains("слиз", StringComparison.Ordinal));
    }

    private static bool TryMatchNormalizedSupport(string symptom, string text)
    {
        return symptom switch
        {
            var value when value.Contains("слизистая мокрота", StringComparison.Ordinal) =>
                text.Contains("слизист", StringComparison.Ordinal) && text.Contains("мокрот", StringComparison.Ordinal),

            var value when value.Contains("слизисто-гнойная мокрота", StringComparison.Ordinal) =>
                text.Contains("слизисто", StringComparison.Ordinal)
                && text.Contains("гной", StringComparison.Ordinal)
                && text.Contains("мокрот", StringComparison.Ordinal),

            var value when value.Contains("одышка при физической активности", StringComparison.Ordinal) =>
                text.Contains("одыш", StringComparison.Ordinal)
                && ((text.Contains("физическ", StringComparison.Ordinal) && text.Contains("нагруз", StringComparison.Ordinal))
                    || text.Contains("при фн", StringComparison.Ordinal)
                    || text.Contains("физической активности", StringComparison.Ordinal)),

            var value when value.Contains("снижение массы тела", StringComparison.Ordinal) =>
                text.Contains("снижение веса", StringComparison.Ordinal)
                || text.Contains("потеря веса", StringComparison.Ordinal)
                || text.Contains("похуд", StringComparison.Ordinal)
                || Regex.IsMatch(text, @"(?:снижение|потеря)\s+веса[^.,;\n]*\d+\s*кг", RegexOptions.IgnoreCase),

            var value when value.Contains("боль в грудной клетке", StringComparison.Ordinal) =>
                text.Contains("бол", StringComparison.Ordinal)
                && text.Contains("грудн", StringComparison.Ordinal)
                && text.Contains("клетк", StringComparison.Ordinal),

            _ => false
        };
    }

    private static bool TryMatchTemperature(string symptom, string text)
    {
        if (!symptom.Contains("температур", StringComparison.Ordinal))
        {
            return false;
        }

        var values = ExtractTemperatureValues(text);
        if (values.Count == 0)
        {
            return symptom.Contains("повышение температуры", StringComparison.Ordinal)
                   && (text.Contains("температур", StringComparison.Ordinal) || text.Contains("лихорад", StringComparison.Ordinal));
        }

        var maxValue = values.Max();
        return symptom switch
        {
            var value when value.Contains("субфебрильная", StringComparison.Ordinal) => maxValue >= 37.1m && maxValue <= 37.9m,
            var value when value.Contains("фебрильная", StringComparison.Ordinal) => maxValue >= 38.0m && maxValue <= 39.5m,
            var value when value.Contains("пиретическая", StringComparison.Ordinal) => maxValue >= 39.6m && maxValue <= 40.9m,
            var value when value.Contains("гиперпиретическая", StringComparison.Ordinal) => maxValue > 41.0m,
            var value when value.Contains("повышение температуры", StringComparison.Ordinal) => true,
            _ => false
        };
    }

    private static List<decimal> ExtractTemperatureValues(string text)
    {
        var values = new List<decimal>();
        var matches = Regex.Matches(text, @"(?<!\d)(?:3[7-9]|4[0-2])(?:[.,]\d)?(?!\d)");

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

        return values;
    }

    private enum SupportState
    {
        Found,
        NotFound,
        Contradicted
    }
}
