using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBuilding.Infrastructure.Persistence;
using SmartBuilding.Infrastructure.Sync;
using SmartBuilding.Shared.DTOs.Api;
using SmartBuilding.Shared.DTOs.Sync;

namespace SmartBuilding.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly SmartBuildingDbContext _context;

    public SyncController(SmartBuildingDbContext context) => _context = context;

    [HttpPost("push")]
    public async Task<ActionResult<ApiResponse<int>>> Push(
        [FromBody] SyncPushRequest request,
        CancellationToken cancellationToken)
    {
        if (SyncEntityRegistry.TryGet(request.EntityType) is null)
            return BadRequest(ApiResponse<int>.Fail($"Type de sync inconnu : {request.EntityType}"));

        var applied = await SyncCoordinator.ApplyPushAsync(_context, request, cancellationToken);
        return Ok(ApiResponse<int>.Ok(applied));
    }

    [HttpGet("pull")]
    public async Task<ActionResult<ApiResponse<SyncPullResponse>>> Pull(
        [FromQuery] string entityType,
        [FromQuery] DateTime since,
        CancellationToken cancellationToken)
    {
        var adapter = SyncEntityRegistry.TryGet(entityType);
        if (adapter is null)
            return BadRequest(ApiResponse<SyncPullResponse>.Fail($"Type de sync inconnu : {entityType}"));

        var entities = await adapter.GetChangesSinceAsync(_context, since, cancellationToken);
        return Ok(ApiResponse<SyncPullResponse>.Ok(new SyncPullResponse
        {
            ServerTimestamp = DateTime.UtcNow,
            Entities = entities.ToList()
        }));
    }
}
