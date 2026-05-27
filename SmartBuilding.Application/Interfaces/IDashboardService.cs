using SmartBuilding.Shared.DTOs.Dashboard;

namespace SmartBuilding.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
}
