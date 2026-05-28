using Microsoft.EntityFrameworkCore;
using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Infrastructure.Persistence;

namespace SmartBuilding.Desktop.WPF.Services;

public class ModuleDataService
{
    private readonly SmartBuildingDbContext _db;

    public ModuleDataService(SmartBuildingDbContext db) => _db = db;

    public async Task<ModuleDataResult> LoadAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        return moduleId switch
        {
            "personnel" => await LoadPersonnelAsync(cancellationToken),
            "locations" => await LoadLocationsAsync(cancellationToken),
            "finances" => await LoadFinancesAsync(cancellationToken),
            "technique" => await LoadTechniqueAsync(cancellationToken),
            "fournisseurs" => await LoadFournisseursAsync(cancellationToken),
            "inventaire" => await LoadInventaireAsync(cancellationToken),
            "consommations" => await LoadConsommationsAsync(cancellationToken),
            "incidents" => await LoadIncidentsAsync(cancellationToken),
            "visites" => await LoadVisitesAsync(cancellationToken),
            "emails" => await LoadEmailsAsync(cancellationToken),
            "documents" => await LoadDocumentsAsync(cancellationToken),
            "utilisateurs" => await LoadUtilisateursAsync(cancellationToken),
            "parametres" => await LoadParametresAsync(cancellationToken),
            "journal" => await LoadJournalAsync(cancellationToken),
            _ => new ModuleDataResult([], [], 0)
        };
    }

    private async Task<ModuleDataResult> LoadPersonnelAsync(CancellationToken ct)
    {
        var items = await _db.Employees.OrderBy(e => e.LastName).Take(500).ToListAsync(ct);
        var rows = items.Select(e => ModuleListRow.From(
            $"{e.FirstName} {e.LastName}",
            e.Matricule,
            e.Position,
            e.Department,
            e.IsActive ? "Actif" : "Inactif",
            MoneyFormatter.Format(e.BaseSalary))).ToList();
        return new(["Nom", "Matricule", "Poste", "Département", "Statut", "Salaire base"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadLocationsAsync(CancellationToken ct)
    {
        var premises = await _db.Premises.OrderBy(p => p.Code).Take(300).ToListAsync(ct);
        var rows = premises.Select(p => ModuleListRow.From(
            p.Code,
            p.Name,
            p.Floor,
            $"{p.AreaSqM:N1} m²",
            p.IsOccupied ? "Occupé" : "Libre",
            MoneyFormatter.Format(p.MonthlyRent))).ToList();
        return new(["Code", "Local", "Étage", "Surface", "Statut", "Loyer/mois"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadFinancesAsync(CancellationToken ct)
    {
        var items = await _db.FinancialTransactions
            .OrderByDescending(t => t.TransactionDate)
            .Take(500)
            .ToListAsync(ct);
        var rows = items.Select(t => ModuleListRow.From(
            t.TransactionDate.ToString("dd/MM/yyyy"),
            t.Type.ToString(),
            t.Category,
            t.Description,
            MoneyFormatter.Format(t.Amount),
            t.Reference ?? "—")).ToList();
        return new(["Date", "Type", "Catégorie", "Description", "Montant", "Référence"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadTechniqueAsync(CancellationToken ct)
    {
        var equipment = await _db.Equipment.OrderBy(e => e.Name).Take(300).ToListAsync(ct);
        var rows = equipment.Select(e => ModuleListRow.From(
            e.Name,
            e.Category,
            e.Location,
            e.Status.ToString(),
            e.LastMaintenanceDate?.ToString("dd/MM/yyyy") ?? "—",
            e.NextMaintenanceDate?.ToString("dd/MM/yyyy") ?? "—")).ToList();
        return new(["Équipement", "Catégorie", "Emplacement", "Statut", "Dernière maintenance", "Prochaine"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadFournisseursAsync(CancellationToken ct)
    {
        var items = await _db.Suppliers.OrderBy(s => s.Name).Take(300).ToListAsync(ct);
        var rows = items.Select(s => ModuleListRow.From(
            s.Name,
            s.Email,
            s.Phone,
            s.Address ?? "—",
            s.TaxId ?? "—",
            "Actif")).ToList();
        return new(["Fournisseur", "Email", "Téléphone", "Adresse", "N° fiscal", "Statut"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadInventaireAsync(CancellationToken ct)
    {
        var items = await _db.InventoryItems.OrderBy(i => i.Name).Take(500).ToListAsync(ct);
        var rows = items.Select(i => ModuleListRow.From(
            i.Code,
            i.Name,
            i.Category,
            i.Location,
            i.Quantity.ToString(),
            MoneyFormatter.Format(i.UnitValue))).ToList();
        return new(["Code", "Article", "Catégorie", "Emplacement", "Quantité", "Valeur"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadConsommationsAsync(CancellationToken ct)
    {
        var items = await _db.ConsumptionRecords
            .OrderByDescending(c => c.PeriodEnd)
            .Take(500)
            .ToListAsync(ct);
        var rows = items.Select(c => ModuleListRow.From(
            c.Type.ToString(),
            c.PeriodStart.ToString("dd/MM/yyyy"),
            c.PeriodEnd.ToString("dd/MM/yyyy"),
            $"{c.Quantity:N2}",
            MoneyFormatter.Format(c.Cost),
            c.Unit)).ToList();
        return new(["Type", "Début", "Fin", "Quantité", "Coût", "Unité"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadIncidentsAsync(CancellationToken ct)
    {
        var items = await _db.Incidents.OrderByDescending(i => i.ReportedAt).Take(500).ToListAsync(ct);
        var rows = items.Select(i => ModuleListRow.From(
            i.Title,
            i.Severity.ToString(),
            i.Status.ToString(),
            i.Location,
            i.ReportedAt.ToString("dd/MM/yyyy HH:mm"),
            MoneyFormatter.Format(i.Cost))).ToList();
        return new(["Titre", "Gravité", "Statut", "Lieu", "Signalé le", "Coût"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadVisitesAsync(CancellationToken ct)
    {
        var items = await _db.Visitors.OrderByDescending(v => v.CheckInAt).Take(500).ToListAsync(ct);
        var rows = items.Select(v => ModuleListRow.From(
            v.FullName,
            v.Company ?? "—",
            v.Purpose,
            v.HostName,
            v.CheckInAt.ToString("dd/MM/yyyy HH:mm"),
            v.CheckOutAt?.ToString("dd/MM/yyyy HH:mm") ?? "En cours")).ToList();
        return new(["Visiteur", "Société", "Motif", "Hôte", "Entrée", "Sortie"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadEmailsAsync(CancellationToken ct)
    {
        var items = await _db.CachedEmails.OrderByDescending(e => e.ReceivedAt).Take(500).ToListAsync(ct);
        var rows = items.Select(e => ModuleListRow.From(
            e.FromAddress,
            e.Subject,
            e.ReceivedAt.ToString("dd/MM/yyyy HH:mm"),
            e.IsRead ? "Lu" : "Non lu",
            e.HasAttachments ? "Oui" : "Non",
            e.Folder)).ToList();
        return new(["Expéditeur", "Objet", "Reçu le", "Lu", "Pièces jointes", "Dossier"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadDocumentsAsync(CancellationToken ct)
    {
        var items = await _db.InventoryItems
            .Where(i => i.Category.ToLower().Contains("doc") || i.Category.ToLower().Contains("fichier"))
            .OrderBy(i => i.Name)
            .Take(500)
            .ToListAsync(ct);

        if (items.Count == 0)
        {
            items = await _db.InventoryItems.OrderBy(i => i.Name).Take(100).ToListAsync(ct);
        }

        var rows = items.Select(i => ModuleListRow.From(
            i.Name,
            i.Category,
            i.Location,
            i.Quantity.ToString(),
            "—",
            "Stock / référence")).ToList();
        return new(["Document", "Type", "Emplacement", "Réf.", "Version", "Source"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadUtilisateursAsync(CancellationToken ct)
    {
        var items = await _db.Users.OrderBy(u => u.Username).Take(200).ToListAsync(ct);
        var rows = items.Select(u => ModuleListRow.From(
            u.Username,
            u.FullName,
            u.Email,
            u.Role.ToString(),
            u.IsActive ? "Actif" : "Inactif",
            u.LastLoginAt?.ToString("dd/MM/yyyy HH:mm") ?? "Jamais")).ToList();
        return new(["Identifiant", "Nom", "Email", "Rôle", "Statut", "Dernière connexion"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadParametresAsync(CancellationToken ct)
    {
        var items = await _db.BuildingInfos.ToListAsync(ct);
        var rows = items.Select(b => ModuleListRow.From(
            b.Name,
            b.Address,
            b.City,
            b.Country,
            b.TotalFloors.ToString(),
            $"{b.TotalAreaSqM:N0} m²")).ToList();
        return new(["Bâtiment", "Adresse", "Ville", "Pays", "Étages", "Surface totale"], rows, rows.Count);
    }

    private async Task<ModuleDataResult> LoadJournalAsync(CancellationToken ct)
    {
        var systemLogs = await _db.SystemLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(250)
            .Select(l => new { l.CreatedAt, l.Level, l.Message, l.Source })
            .ToListAsync(ct);

        var syncLogs = await _db.SyncLogs
            .OrderByDescending(l => l.StartedAt)
            .Take(250)
            .Select(l => new { l.StartedAt, Success = l.Success, l.ErrorMessage, l.RecordsPushed, l.RecordsPulled })
            .ToListAsync(ct);

        var rows = systemLogs.Select(l => ModuleListRow.From(
            l.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
            "Système",
            l.Level,
            l.Source,
            l.Message.Length > 80 ? l.Message[..80] + "…" : l.Message,
            "")).ToList();

        rows.AddRange(syncLogs.Select(l => ModuleListRow.From(
            l.StartedAt.ToString("dd/MM/yyyy HH:mm"),
            "Sync",
            l.Success ? "OK" : "Erreur",
            $"{l.RecordsPushed}/{l.RecordsPulled}",
            l.ErrorMessage ?? "Synchronisation",
            "")));

        rows = rows.OrderByDescending(r => r.Col0).Take(500).ToList();
        return new(["Date", "Type", "Niveau", "Source", "Message", ""], rows, rows.Count);
    }
}
