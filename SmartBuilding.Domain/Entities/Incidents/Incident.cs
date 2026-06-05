using SmartBuilding.Domain.Common;
using SmartBuilding.Domain.Enums;

namespace SmartBuilding.Domain.Entities.Incidents;

public class Incident : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Ouvert;
    public string Location { get; set; } = string.Empty;
    public Guid? EquipmentId { get; set; }
    public string Building { get; set; } = string.Empty;
    public string Responsible { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "Moyen";
    public DateTime ReportedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public decimal Cost { get; set; }
    public Guid? ReportedByUserId { get; set; }
    public string? ResolutionNotes { get; set; }
    public bool HasPhoto { get; set; }

    public ICollection<IncidentIntervention> Interventions { get; set; } = [];
}
