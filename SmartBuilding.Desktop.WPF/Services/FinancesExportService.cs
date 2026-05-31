using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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

    public static bool PrintTransactionsList(IEnumerable<FinanceTransactionItem> items, string title)
    {
        var table = new Table { CellSpacing = 0 };
        foreach (var _ in new[] { 75, 70, 55, 80, 140, 70, 65, 60 })
            table.Columns.Add(new TableColumn());

        var header = new TableRowGroup();
        var headerRow = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.SemiBold };
        foreach (var h in new[] { "Référence", "Date", "Type", "Catégorie", "Description", "Source", "Montant", "Statut" })
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(h))) { Padding = new Thickness(4) });
        header.Rows.Add(headerRow);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var t in items)
        {
            var row = new TableRow();
            foreach (var v in new[] { t.Reference, t.DateDisplay, t.TypeLabel, t.Category, t.Description,
                         t.Source, t.AmountDisplay, t.StatusLabel })
                row.Cells.Add(new TableCell(new Paragraph(new Run(v))) { Padding = new Thickness(4) });
            body.Rows.Add(row);
        }
        table.RowGroups.Add(body);

        var doc = new FlowDocument(
            new Paragraph(new Run(title)) { FontSize = 16, FontWeight = FontWeights.Bold })
        {
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 9
        };
        doc.Blocks.Add(table);

        var pd = new PrintDialog();
        if (pd.ShowDialog() != true)
            return false;

        pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, title);
        return true;
    }
}
