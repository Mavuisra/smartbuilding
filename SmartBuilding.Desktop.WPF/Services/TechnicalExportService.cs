using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class TechnicalExportService
{
    public static bool ExportCsv(IEnumerable<TechnicalEquipmentItem> items)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel CSV (*.csv)|*.csv",
            FileName = $"equipements_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dlg.ShowDialog() != true)
            return false;

        var path = dlg.FileName;
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
        return true;
    }

    public static bool PrintEquipmentList(IEnumerable<TechnicalEquipmentItem> items, string title)
    {
        var table = new Table { CellSpacing = 0 };
        foreach (var _ in new[] { 80, 120, 90, 100, 80, 80, 80, 70 })
            table.Columns.Add(new TableColumn());

        var header = new TableRowGroup();
        var headerRow = new TableRow { Background = System.Windows.Media.Brushes.LightGray, FontWeight = FontWeights.SemiBold };
        foreach (var h in new[] { "Code", "Équipement", "Catégorie", "Emplacement", "Statut", "Dern. maint.", "Proch. maint.", "Coût" })
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(h))) { Padding = new Thickness(5) });
        header.Rows.Add(headerRow);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var e in items)
        {
            var row = new TableRow();
            foreach (var v in new[] { e.Code, e.Name, e.Category, e.Location, e.StatusLabel,
                         e.LastMaintenanceDisplay, e.NextMaintenanceDisplay, e.MaintenanceCostDisplay })
                row.Cells.Add(new TableCell(new Paragraph(new Run(v))) { Padding = new Thickness(5) });
            body.Rows.Add(row);
        }
        table.RowGroups.Add(body);

        var doc = new FlowDocument(
            new Paragraph(new Run(title)) { FontSize = 16, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.Black })
        {
            PagePadding = new Thickness(40),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 10
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
