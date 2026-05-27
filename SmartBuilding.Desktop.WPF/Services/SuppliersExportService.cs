using System.IO;
using System.Text;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class SuppliersExportService
{
    public static string ExportCsv(IEnumerable<SupplierListItem> items)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Exports");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"fournisseurs_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("Code;Entreprise;Categorie;Telephone;Email;Contrat;Depenses_FC;Derniere_intervention;Statut");

        foreach (var s in items)
        {
            sb.Append(Csv(s.Code)).Append(';')
                .Append(Csv(s.Name)).Append(';')
                .Append(Csv(s.Category)).Append(';')
                .Append(Csv(s.Phone)).Append(';')
                .Append(Csv(s.Email)).Append(';')
                .Append(Csv(s.ContractDisplay)).Append(';')
                .Append(Csv(s.TotalExpensesDisplay)).Append(';')
                .Append(Csv(s.LastInterventionDisplay)).Append(';')
                .AppendLine(Csv(s.StatusLabel));
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
