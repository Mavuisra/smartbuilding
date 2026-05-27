using System.Globalization;
using System.IO;
using System.Text;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class FinancesExportService
{
    public static string ExportCsv(IEnumerable<FinanceTransactionItem> items)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Exports");
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, $"finances_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("Reference;Date;Type;Categorie;Description;Source;Mode;Montant_FC;Statut;Utilisateur");

        foreach (var t in items)
        {
            sb.Append(Csv(t.Reference)).Append(';')
                .Append(Csv(t.DateDisplay)).Append(';')
                .Append(Csv(t.TypeLabel)).Append(';')
                .Append(Csv(t.Category)).Append(';')
                .Append(Csv(t.Description)).Append(';')
                .Append(Csv(t.Source)).Append(';')
                .Append(Csv(t.PaymentMethod)).Append(';')
                .Append(t.Amount.ToString(CultureInfo.InvariantCulture)).Append(';')
                .Append(Csv(t.StatusLabel)).Append(';')
                .AppendLine(Csv(t.RecordedBy));
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
