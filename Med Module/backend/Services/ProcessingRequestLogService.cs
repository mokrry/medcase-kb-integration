using System.Text.Json;
using MedicalFeaturePrototype.Api.Data;
using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Entities;
using MedicalFeaturePrototype.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MedicalFeaturePrototype.Api.Services;

public class ProcessingRequestLogService : IProcessingRequestLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly ApplicationDbContext _dbContext;

    public ProcessingRequestLogService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreatePromptBundleStartedAsync(
        Guid userId,
        string requestId,
        string sourceText,
        CancellationToken cancellationToken = default)
    {
        var existingRequest = await _dbContext.ProcessingRequests
            .FirstOrDefaultAsync(
                request => request.UserId == userId && request.RequestId == requestId,
                cancellationToken);

        if (existingRequest is not null)
        {
            existingRequest.Status = ProcessingRequestStatuses.Started;
            existingRequest.SourceText = sourceText;
            existingRequest.ErrorMessage = string.Empty;
            existingRequest.FinishedAt = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var request = new ProcessingRequest
        {
            UserId = userId,
            RequestId = requestId,
            Status = ProcessingRequestStatuses.Started,
            InternalMode = "prepare-extract-vote-evidence-solver",
            UsedVoting = true,
            SourceText = sourceText,
            PreparedText = string.Empty,
            FinalSymptomsJson = "[]",
            EvidenceJson = "{}",
            ManualChangesJson = "{}",
            SolverPayloadJson = "{}",
            SolverResponseJson = string.Empty,
            ErrorMessage = string.Empty,
            FinishedAt = null
        };

        _dbContext.ProcessingRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompletePromptBundleAsync(
        Guid userId,
        PromptRunBundleResponseDto response,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ProcessingRequests
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.RequestId == response.PromptBuild.RequestId,
                cancellationToken);

        if (request is null)
        {
            request = new ProcessingRequest
            {
                UserId = userId,
                RequestId = response.PromptBuild.RequestId
            };

            _dbContext.ProcessingRequests.Add(request);
        }

        request.Status = ProcessingRequestStatuses.Completed;
        request.InternalMode = "prepare-extract-vote-evidence-solver";
        request.UsedVoting = true;
        request.SourceText = response.PromptBuild.SourceComplaintsText;
        request.PreparedText = response.Voting.PreparedComplaintsText;
        request.FinalSymptomsJson = JsonSerializer.Serialize(response.Voting.FinalSymptoms, JsonOptions);
        request.EvidenceJson = JsonSerializer.Serialize(response.EvidenceVerification, JsonOptions);
        request.SolverPayloadJson = "{}";
        request.SolverResponseJson = string.Empty;
        request.ErrorMessage = string.Empty;
        request.FinishedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FailPromptBundleAsync(
        Guid userId,
        string requestId,
        string sourceText,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ProcessingRequests
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.RequestId == requestId,
                cancellationToken);

        if (request is null)
        {
            request = new ProcessingRequest
            {
                UserId = userId,
                RequestId = requestId
            };

            _dbContext.ProcessingRequests.Add(request);
        }

        request.Status = ProcessingRequestStatuses.Failed;
        request.InternalMode = "prepare-extract-vote-evidence-solver";
        request.UsedVoting = true;
        request.SourceText = sourceText;
        request.ErrorMessage = errorMessage;
        request.FinishedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateManualSolveStartedAsync(
        Guid userId,
        string requestId,
        string sourceText,
        IReadOnlyList<string> symptoms,
        CancellationToken cancellationToken = default)
    {
        var existingRequest = await _dbContext.ProcessingRequests
            .FirstOrDefaultAsync(
                request => request.UserId == userId && request.RequestId == requestId,
                cancellationToken);

        if (existingRequest is not null)
        {
            existingRequest.Status = ProcessingRequestStatuses.Started;
            existingRequest.InternalMode = "manual-symptoms-solver";
            existingRequest.UsedVoting = false;
            existingRequest.SourceText = sourceText;
            existingRequest.FinalSymptomsJson = JsonSerializer.Serialize(symptoms, JsonOptions);
            existingRequest.ManualChangesJson = JsonSerializer.Serialize(new { symptoms }, JsonOptions);
            existingRequest.SolverPayloadJson = "{}";
            existingRequest.SolverResponseJson = string.Empty;
            existingRequest.ErrorMessage = string.Empty;
            existingRequest.FinishedAt = null;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var request = new ProcessingRequest
        {
            UserId = userId,
            RequestId = requestId,
            Status = ProcessingRequestStatuses.Started,
            InternalMode = "manual-symptoms-solver",
            UsedVoting = false,
            SourceText = sourceText,
            FinalSymptomsJson = JsonSerializer.Serialize(symptoms, JsonOptions),
            ManualChangesJson = JsonSerializer.Serialize(new { symptoms }, JsonOptions),
            SolverPayloadJson = "{}",
            SolverResponseJson = string.Empty,
            ErrorMessage = string.Empty,
            FinishedAt = null
        };

        _dbContext.ProcessingRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteManualSolveAsync(
        Guid userId,
        string requestId,
        string sourceText,
        IReadOnlyList<string> symptoms,
        KnowledgeBaseSolverDto solver,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ProcessingRequests
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.RequestId == requestId,
                cancellationToken);

        if (request is null)
        {
            request = new ProcessingRequest
            {
                UserId = userId,
                RequestId = requestId
            };

            _dbContext.ProcessingRequests.Add(request);
        }

        request.Status = solver.IsSuccess ? ProcessingRequestStatuses.Completed : ProcessingRequestStatuses.Failed;
        request.InternalMode = "manual-symptoms-solver";
        request.UsedVoting = false;
        request.SourceText = sourceText;
        request.FinalSymptomsJson = JsonSerializer.Serialize(symptoms, JsonOptions);
        request.ManualChangesJson = JsonSerializer.Serialize(new { symptoms }, JsonOptions);
        request.SolverPayloadJson = solver.RequestJson;
        request.SolverResponseJson = solver.ResponseJson;
        request.ErrorMessage = solver.IsSuccess ? string.Empty : solver.Error;
        request.FinishedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FailManualSolveAsync(
        Guid userId,
        string requestId,
        string sourceText,
        IReadOnlyList<string> symptoms,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.ProcessingRequests
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.RequestId == requestId,
                cancellationToken);

        if (request is null)
        {
            request = new ProcessingRequest
            {
                UserId = userId,
                RequestId = requestId
            };

            _dbContext.ProcessingRequests.Add(request);
        }

        request.Status = ProcessingRequestStatuses.Failed;
        request.InternalMode = "manual-symptoms-solver";
        request.UsedVoting = false;
        request.SourceText = sourceText;
        request.FinalSymptomsJson = JsonSerializer.Serialize(symptoms, JsonOptions);
        request.ManualChangesJson = JsonSerializer.Serialize(new { symptoms }, JsonOptions);
        request.ErrorMessage = errorMessage;
        request.FinishedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessingRequestListItemDto>> GetListAsync(
        Guid userId,
        bool includeAllUsers,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = FilterRequests(userId, includeAllUsers, status, dateFrom, dateTo);

        return await query
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => new ProcessingRequestListItemDto
            {
                Id = request.Id,
                RequestId = request.RequestId,
                Status = request.Status,
                InternalMode = request.InternalMode,
                UsedVoting = request.UsedVoting,
                CreatedAt = request.CreatedAt,
                FinishedAt = request.FinishedAt,
                ErrorMessage = request.ErrorMessage
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProcessingRequestDetailsDto?> GetDetailsAsync(
        Guid userId,
        bool includeAllUsers,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProcessingRequests
            .Where(request => request.Id == id && (includeAllUsers || request.UserId == userId))
            .Select(request => new ProcessingRequestDetailsDto
            {
                Id = request.Id,
                RequestId = request.RequestId,
                Status = request.Status,
                InternalMode = request.InternalMode,
                UsedVoting = request.UsedVoting,
                CreatedAt = request.CreatedAt,
                FinishedAt = request.FinishedAt,
                ErrorMessage = request.ErrorMessage,
                SourceText = request.SourceText,
                PreparedText = request.PreparedText,
                FinalSymptomsJson = request.FinalSymptomsJson,
                EvidenceJson = request.EvidenceJson,
                ManualChangesJson = request.ManualChangesJson,
                SolverPayloadJson = request.SolverPayloadJson,
                SolverResponseJson = request.SolverResponseJson
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<ProcessingRequest> FilterRequests(
        Guid userId,
        bool includeAllUsers,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        var query = _dbContext.ProcessingRequests.AsNoTracking();

        if (!includeAllUsers)
        {
            query = query.Where(request => request.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(request => request.Status == status);
        }

        if (dateFrom is not null)
        {
            query = query.Where(request => request.CreatedAt >= dateFrom.Value);
        }

        if (dateTo is not null)
        {
            query = query.Where(request => request.CreatedAt <= dateTo.Value);
        }

        return query;
    }
}
