using MedicalFeaturePrototype.Api.Dtos;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IKnowledgeBaseSolverService
{
    Task<KnowledgeBaseSolverDto> BuildAndSolveAsync(
        IReadOnlyList<string> finalSymptoms,
        string preparedComplaintsText,
        CancellationToken cancellationToken = default);
}
