using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class UsersExportService
{
    public static string ExportPdf(IEnumerable<UserListItem> users)
    {
        var list = users.ToList();
        return PdfListExportService.Export(
            moduleFolder: "Utilisateurs",
            filePrefix: "utilisateurs",
            documentTitle: "Liste des utilisateurs",
            documentSubtitle: "Comptes et accès SBMS",
            headers: ["Identifiant", "Nom", "Email", "Rôle", "Département", "Statut", "Dernière connexion"],
            rows: list.Select(u => new[]
            {
                u.Username, u.FullName, u.Email, u.RoleLabel, u.Department, u.StatusLabel, u.LastLoginDisplay
            }),
            kpis: [("Utilisateurs", list.Count.ToString())]);
    }

    public static string ExportCsv(IEnumerable<UserListItem> users) => ExportPdf(users);
}
