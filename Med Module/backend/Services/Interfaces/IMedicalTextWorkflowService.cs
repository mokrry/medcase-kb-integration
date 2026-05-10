using MedicalFeaturePrototype.Api.Models;
using MedicalFeaturePrototype.Api.Dtos;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IMedicalTextWorkflowService
{
    Task<MedicalTextPreparationResult> PrepareMedicalTextAsync(string complaintsText, string requestId, CancellationToken cancellationToken = default);
    Task<DisputedSymptomsResolutionResult> ResolveDisputedSymptomsAsync(
        string preparedComplaintsText,
        IReadOnlyList<string> matchedSymptoms,
        IReadOnlyList<string> disputedSymptoms,
        IReadOnlyList<string> whitelist,
        string requestId,
        CancellationToken cancellationToken = default);
    Task<SymptomEvidenceVerificationDto> VerifySymptomEvidenceAsync(
        string sourceComplaintsText,
        string preparedComplaintsText,
        IReadOnlyList<string> finalSymptoms,
        string requestId,
        CancellationToken cancellationToken = default);
}
