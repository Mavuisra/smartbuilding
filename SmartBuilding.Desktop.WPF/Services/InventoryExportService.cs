using System.IO;
using System.Text;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class InventoryExportService
{
    public static string ExportCsv(IEnumerable<InventoryListItem> items)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Exports");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"inventaire_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("Code;Nom;Categorie;Emplacement;Etat;Responsable;Derniere_maint;Prochaine_maint;Valeur_FC");

        foreach (var i in items)
        {
            sb.Append(Csv(i.Code)).Append(';')
                .Append(Csv(i.Name)).Append(';')
                .Append(Csv(i.Category)).Append(';')
                .Append(Csv(i.Location)).Append(';')
                .Append(Csv(i.StatusLabel)).Append(';')
                .Append(Csv(i.Responsible)).Append(';')
                .Append(Csv(i.LastMaintenanceDisplay)).Append(';')
                .Append(Csv(i.NextMaintenanceDisplay)).Append(';')
                .AppendLine(Csv(i.EstimatedValueDisplay));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    private static string Csv(string? value)
    {
        var v = value ?? "";
        return v.Contains(';') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
