using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class IncidentsExportService
{
    public static bool ExportCsv(IEnumerable<IncidentListItem> items)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel CSV (*.csv)|*.csv",
            FileName = $"incidents_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dlg.ShowDialog() != true)
            return false;

        var sb = new StringBuilder();
        sb.AppendLine("ID;Date;Type;Emplacement;Gravité;Responsable;Statut;Coût;Intervention");
        foreach (var i in items)
        {
            sb.AppendLine(string.Join(';',
                Csv(i.Code), Csv(i.DateDisplay), Csv(i.TypeLabel), Csv(i.Location), Csv(i.SeverityLabel),
                Csv(i.Responsible), Csv(i.StatusLabel), Csv(i.CostDisplay), Csv(i.InterventionSummary)));
        }

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        return true;
    }

    public static bool PrintIncidentsList(IEnumerable<IncidentListItem> items, string title)
    {
        var table = new Table { CellSpacing = 0 };
        foreach (var _ in new[] { 70, 95, 90, 100, 70, 85, 80, 65, 85 })
            table.Columns.Add(new TableColumn());

        var header = new TableRowGroup();
        var headerRow = new TableRow { Background = System.Windows.Media.Brushes.LightGray, FontWeight = FontWeights.SemiBold };
        foreach (var h in new[] { "ID", "Date", "Type", "Emplacement", "Gravité", "Responsable", "Statut", "Coût", "Intervention" })
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(h))) { Padding = new Thickness(4) });
        header.Rows.Add(headerRow);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var i in items)
        {
            var row = new TableRow();
            foreach (var v in new[] { i.Code, i.DateDisplay, i.TypeLabel, i.Location, i.SeverityLabel,
                         i.Responsible, i.StatusLabel, i.CostDisplay, i.InterventionSummary })
                row.Cells.Add(new TableCell(new Paragraph(new Run(v))) { Padding = new Thickness(4) });
            body.Rows.Add(row);
        }
        table.RowGroups.Add(body);

        var doc = new FlowDocument(
            new Paragraph(new Run(title)) { FontSize = 16, FontWeight = FontWeights.Bold })
        {
            PagePadding = new Thickness(40),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 9
        };
        doc.Blocks.Add(table);

        var pd = new PrintDialog();
        if (pd.ShowDialog() != true)
            return false;

        pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, title);
        return true;
    }

    private static string Csv(string? value)
    {
        var v = value ?? "";
        return v.Contains(';') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
