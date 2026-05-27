using SmartBuilding.Desktop.WPF.Models;
using SmartBuilding.Shared.Constants;

namespace SmartBuilding.Desktop.WPF.Services;

public static class ModuleRegistry
{
    public static readonly IReadOnlyList<ModuleDefinition> All =
    [
        new("dashboard", "Tableau de bord", "Vue d'ensemble", "ViewDashboard", "main", PermissionCodes.DashboardView),
        new("locations", "Local", "Locaux, locataires et contrats", "HomeCity", "gestion", PermissionCodes.LocationManage),
        new("personnel", "Personnel", "Employés, présences et salaires", "AccountGroup", "gestion", PermissionCodes.PersonnelView),
        new("finances", "Finances", "Recettes, dépenses et trésorerie", "CashMultiple", "gestion", PermissionCodes.FinanceView),
        new("technique", "Technique & Sécurité", "Équipements, maintenance et incidents", "HammerWrench", "gestion", PermissionCodes.TechnicalManage),
        new("fournisseurs", "Fournisseurs", "Partenaires et contrats fournisseurs", "TruckDelivery", "gestion", PermissionCodes.SuppliersManage),
        new("inventaire", "Inventaire Technique", "Parc équipements et maintenance", "PackageVariant", "gestion", PermissionCodes.InventoryManage),
        new("consommations", "Consommations", "Énergie, eau et coûts", "LightningBolt", "gestion", PermissionCodes.ConsumptionManage),
        new("visites", "Visites & Accès", "Visiteurs, accès et réception", "BadgeAccount", "gestion", PermissionCodes.VisitorsManage),
        new("emails", "Emails & Communication", "Boîte mail intégrée Gmail/Outlook", "EmailOutline", "gestion", PermissionCodes.EmailManage),
        new("documents", "Documents", "Fichiers et pièces jointes liés", "FileDocumentOutline", "gestion", PermissionCodes.DashboardView),
        new("utilisateurs", "Utilisateurs", "Comptes et rôles", "AccountKey", "admin", PermissionCodes.UsersManage),
        new("parametres", "Paramètres", "Configuration du bâtiment", "Cog", "admin", PermissionCodes.UsersManage),
        new("synchronisation", "Synchronisation", "État cloud et conflits", "Sync", "admin", PermissionCodes.SyncManage),
        new("journal", "Journal d'activité", "Logs système et synchronisation", "History", "admin", PermissionCodes.SyncManage)
    ];

    private static readonly Dictionary<string, ModuleDefinition> ById =
        All.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

    public static ModuleDefinition Get(string id) =>
        ById.TryGetValue(id, out var module)
            ? module
            : new(id, id, "", "FolderOutline", "gestion");

    public static bool CanAccess(SessionService session, ModuleDefinition module) =>
        module.PermissionCode is null || session.HasPermission(module.PermissionCode);

    public static IEnumerable<ShellNavEntry> BuildNavigation(SessionService session)
    {
        var sectionLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gestion"] = "GESTION",
            ["admin"] = "ADMINISTRATION"
        };

        foreach (var section in new[] { "main", "gestion", "admin" })
        {
            var modules = All.Where(m => m.Section == section && CanAccess(session, m)).ToList();
            if (modules.Count == 0)
                continue;

            if (sectionLabels.TryGetValue(section, out var label))
                yield return new ShellNavSectionHeader(label);

            foreach (var module in modules)
            {
                // Local juste avant Personnel (ordre défini dans All : … locations, personnel …)
                if (string.Equals(module.Id, "locations", StringComparison.OrdinalIgnoreCase)
                    && CanAccess(session, module))
                {
                    yield return new ShellNavExpandableModuleItem(module,
                    [
                        new ShellNavChildItem("locations-create", "Nouveau contrat"),
                        new ShellNavChildItem("locations-list", "Liste des locaux"),
                        new ShellNavChildItem("locations-rent-pay", "Paiement de loyer")
                    ]);
                    continue;
                }

                yield return new ShellNavModuleItem(module);
            }
        }
    }
}
