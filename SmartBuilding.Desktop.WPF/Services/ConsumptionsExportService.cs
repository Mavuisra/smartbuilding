using System.Globalization;
using System.IO;
using System.Text;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class ConsumptionsExportService
{
    public static string ExportCsv(IEnumerable<ConsumptionListItem> items)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Exports");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"consommations_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Date;Type;Poste;Montant;Variation;Responsable;Statut;Bâtiment");
        foreach (var i in items)
        {
            sb.AppendLine(string.Join(';',
                i.DateDisplay,
                i.TypeLabel,
                i.EquipmentSource,
                i.CostDisplay,
                i.VariationDisplay,
                i.Responsible,
                i.StatusLabel,
                i.Building));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }
}
