using System.Security.Claims;
using MedicalFeaturePrototype.Api.Dtos;
using MedicalFeaturePrototype.Api.Entities;
using MedicalFeaturePrototype.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalFeaturePrototype.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/requests")]
public class RequestsController : ControllerBase
{
    private readonly IProcessingRequestLogService _requestLogService;

    public RequestsController(IProcessingRequestLogService requestLogService)
    {
        _requestLogService = requestLogService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProcessingRequestListItemDto>>> GetList(
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var includeAllUsers = User.IsInRole(UserRoles.Admin);
        var result = await _requestLogService.GetListAsync(
            userId.Value,
            includeAllUsers,
            status,
            dateFrom,
            dateTo,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProcessingRequestDetailsDto>> GetDetails(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var includeAllUsers = User.IsInRole(UserRoles.Admin);
        var result = await _requestLogService.GetDetailsAsync(
            userId.Value,
            includeAllUsers,
            id,
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
