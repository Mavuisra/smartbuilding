using System.Globalization;
using System.IO;
using System.Text;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class TechnicalExportService
{
    public static string ExportCsv(IEnumerable<TechnicalEquipmentItem> items)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Exports");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"technique_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("Code;Nom;Categorie;Emplacement;Statut;Derniere_maint;Prochaine_maint;Cout_FC");

        foreach (var e in items)
        {
            sb.Append(Csv(e.Code)).Append(';')
                .Append(Csv(e.Name)).Append(';')
                .Append(Csv(e.Category)).Append(';')
                .Append(Csv(e.Location)).Append(';')
                .Append(Csv(e.StatusLabel)).Append(';')
                .Append(Csv(e.LastMaintenanceDisplay)).Append(';')
                .Append(Csv(e.NextMaintenanceDisplay)).Append(';')
                .AppendLine(Csv(e.MaintenanceCostDisplay));
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
