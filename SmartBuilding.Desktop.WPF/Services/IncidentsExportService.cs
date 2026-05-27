using System.IO;
using System.Text;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class IncidentsExportService
{
    public static string ExportCsv(IEnumerable<IncidentListItem> items)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Exports");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"incidents_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("ID;Date;Type;Emplacement;Gravité;Responsable;Statut;Coût;Intervention");
        foreach (var i in items)
        {
            sb.AppendLine(string.Join(';',
                i.Code, i.DateDisplay, i.TypeLabel, i.Location, i.SeverityLabel,
                i.Responsible, i.StatusLabel, i.CostDisplay, i.InterventionSummary));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }
}
