using SmartBuilding.Domain.Common;

namespace SmartBuilding.Domain.Entities.Visitors;

/// <summary>Rendez-vous visiteur (pas de réservation de salle).</summary>
public class VisitorAppointment : BaseEntity
{
    public Guid? VisitorId { get; set; }
    public string VisitorName { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string Room { get; set; } = string.Empty;
    public string Building { get; set; } = "Tour SBMS";
    public string Status { get; set; } = "Confirmé";
    public int DurationMinutes { get; set; } = 60;
}
