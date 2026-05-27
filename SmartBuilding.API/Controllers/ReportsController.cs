using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartBuilding.Application.Interfaces;

namespace SmartBuilding.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService) => _reportService = reportService;

    [HttpGet("financial/pdf")]
    public async Task<IActionResult> FinancialPdf([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var bytes = await _reportService.GenerateFinancialPdfAsync(year, month, ct);
        return File(bytes, "application/pdf", $"rapport-financier-{year}-{month:D2}.pdf");
    }

    [HttpGet("financial/excel")]
    public async Task<IActionResult> FinancialExcel([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        var bytes = await _reportService.GenerateFinancialExcelAsync(year, month, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"rapport-financier-{year}-{month:D2}.xlsx");
    }
}
