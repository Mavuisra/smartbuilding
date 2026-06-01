using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class VisitsExportService
{
    public static string ExportCsv(IReadOnlyList<VisitListItem> visits)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SBMS", "Exports");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"visites_{DateTime.Now:yyyyMMdd_HHmm}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Code;Visiteur;Téléphone;Personne visitée;Motif;Type;Entrée;Sortie;Statut;Badge;Zone");
        foreach (var v in visits)
        {
            sb.Append(Csv(v.VisitCode)).Append(';')
                .Append(Csv(v.FullName)).Append(';')
                .Append(Csv(v.Phone)).Append(';')
                .Append(Csv(v.HostName)).Append(';')
                .Append(Csv(v.Purpose)).Append(';')
                .Append(Csv(v.VisitType)).Append(';')
                .Append(Csv(v.CheckInDisplay)).Append(';')
                .Append(Csv(v.CheckOutDisplay)).Append(';')
                .Append(Csv(v.AccessStatus)).Append(';')
                .Append(Csv(v.BadgeNumber)).Append(';')
                .AppendLine(Csv(v.Zone));
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    public static bool PrintPdfReport(IReadOnlyList<VisitListItem> visits, string title)
    {
        var table = new Table { CellSpacing = 0 };
        foreach (var _ in new[] { 70, 120, 90, 100, 80, 90, 90, 70 })
            table.Columns.Add(new TableColumn());

        var header = new TableRowGroup();
        var headerRow = new TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.SemiBold };
        foreach (var h in new[] { "Code", "Visiteur", "Téléphone", "Hôte", "Motif", "Entrée", "Sortie", "Accès" })
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(h))) { Padding = new Thickness(4) });
        header.Rows.Add(headerRow);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var v in visits)
        {
            var row = new TableRow();
            foreach (var cell in new[] { v.VisitCode, v.FullName, v.Phone, v.HostName, v.Purpose,
                         v.CheckInDisplay, v.CheckOutDisplay, v.AccessStatus })
                row.Cells.Add(new TableCell(new Paragraph(new Run(cell))) { Padding = new Thickness(4) });
            body.Rows.Add(row);
        }
        table.RowGroups.Add(body);

        var doc = new FlowDocument
        {
            PagePadding = new Thickness(40),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 9
        };
        doc.Blocks.Add(new Paragraph(new Run(title)) { FontSize = 16, FontWeight = FontWeights.Bold });
        doc.Blocks.Add(new Paragraph(new Run($"Généré le {DateTime.Now:dd/MM/yyyy HH:mm} — {visits.Count} enregistrement(s)"))
        {
            FontSize = 10,
            Foreground = Brushes.Gray,
            Margin = new Thickness(0, 0, 0, 12)
        });
        doc.Blocks.Add(table);

        var pd = new PrintDialog();
        if (pd.ShowDialog() != true)
            return false;

        pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, title);
        return true;
    }

    private static string Csv(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
