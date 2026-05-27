using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Visitors;

public class Visitor : BaseEntity
{
    public string VisitCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? IdDocument { get; set; }
    public string IdDocumentType { get; set; } = "CNI";
    public string HostName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string VisitType { get; set; } = "Réunion";
    public string AccessStatus { get; set; } = "Actif";
    public string Building { get; set; } = "Tour SBMS";
    public string Zone { get; set; } = "Réception";
    public string AllowedZones { get; set; } = "Réception,Hall principal";
    public DateTime CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public DateTime? ExpectedCheckOutAt { get; set; }
    public string? BadgeNumber { get; set; }
    public string? Notes { get; set; }
}
