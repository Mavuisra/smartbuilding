using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Incidents;

public class IncidentIntervention : BaseEntity
{
    public Guid IncidentId { get; set; }
    public string Technician { get; set; } = string.Empty;
    public string InterventionType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public decimal Cost { get; set; }
    public string Status { get; set; } = "En cours";
    public string Result { get; set; } = string.Empty;

    public Incident Incident { get; set; } = null!;
}
