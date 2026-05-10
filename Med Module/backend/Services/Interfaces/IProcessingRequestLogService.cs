using MedicalFeaturePrototype.Api.Dtos;

namespace MedicalFeaturePrototype.Api.Services.Interfaces;

public interface IProcessingRequestLogService
{
    Task CreatePromptBundleStartedAsync(
        Guid userId,
        string requestId,
        string sourceText,
        CancellationToken cancellationToken = default);

    Task CompletePromptBundleAsync(
        Guid userId,
        PromptRunBundleResponseDto response,
        CancellationToken cancellationToken = default);

    Task FailPromptBundleAsync(
        Guid userId,
        string requestId,
        string sourceText,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task CreateManualSolveStartedAsync(
        Guid userId,
        string requestId,
        string sourceText,
        IReadOnlyList<string> symptoms,
        CancellationToken cancellationToken = default);

    Task CompleteManualSolveAsync(
        Guid userId,
        string requestId,
        string sourceText,
        IReadOnlyList<string> symptoms,
        KnowledgeBaseSolverDto solver,
        CancellationToken cancellationToken = default);

    Task FailManualSolveAsync(
        Guid userId,
        string requestId,
        string sourceText,
        IReadOnlyList<string> symptoms,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessingRequestListItemDto>> GetListAsync(
        Guid userId,
        bool includeAllUsers,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<ProcessingRequestDetailsDto?> GetDetailsAsync(
        Guid userId,
        bool includeAllUsers,
        Guid id,
        CancellationToken cancellationToken = default);
}
