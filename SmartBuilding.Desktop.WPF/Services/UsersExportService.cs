using System.Globalization;
using System.IO;
using System.Text;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class UsersExportService
{
    public static string ExportCsv(IEnumerable<UserListItem> users)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Exports");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"utilisateurs_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("Identifiant;Nom;Email;Role;Departement;Statut;Derniere_connexion");

        foreach (var u in users)
        {
            sb.Append(Csv(u.Username)).Append(';')
                .Append(Csv(u.FullName)).Append(';')
                .Append(Csv(u.Email)).Append(';')
                .Append(Csv(u.RoleLabel)).Append(';')
                .Append(Csv(u.Department)).Append(';')
                .Append(Csv(u.StatusLabel)).Append(';')
                .AppendLine(Csv(u.LastLoginDisplay));
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    private static string Csv(string? value)
    {
        var v = value ?? "";
        return v.Contains(';') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
