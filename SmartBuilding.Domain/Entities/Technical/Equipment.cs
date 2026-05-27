using SmartBuilding.Domain.Common;
using SmartBuilding.Domain.Enums;

namespace SmartBuilding.Domain.Entities.Technical;

public class Equipment : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Operationnel;
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime? InstallationDate { get; set; }
    public decimal PurchaseValue { get; set; }
    public DateTime? WarrantyUntil { get; set; }
    public string PowerSpec { get; set; } = string.Empty;
    public string VoltageSpec { get; set; } = string.Empty;
    public string FrequencySpec { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;

    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = [];
    public ICollection<RepairRecord> RepairRecords { get; set; } = [];
}
