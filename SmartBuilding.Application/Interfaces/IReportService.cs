namespace SmartBuilding.Application.Interfaces;

public interface IReportService
{
    Task<byte[]> GenerateFinancialPdfAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateFinancialExcelAsync(int year, int month, CancellationToken cancellationToken = default);
}
