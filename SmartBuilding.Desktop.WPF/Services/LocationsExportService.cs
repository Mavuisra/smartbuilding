using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;
using SmartBuilding.Desktop.WPF.Models;

namespace SmartBuilding.Desktop.WPF.Services;

public static class LocationsExportService
{
    public static bool ExportPremisesCsv(IEnumerable<LocationsPremiseItem> rows)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Excel CSV (*.csv)|*.csv",
            FileName = $"locaux_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dlg.ShowDialog() != true)
            return false;

        var sb = new StringBuilder();
        sb.AppendLine("Code;Nom;Batiment;Etage;Type;Locataire;Telephone;Loyer_FC;Statut;Fin_contrat");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(';',
                Csv(r.Code), Csv(r.Name), Csv(r.Building), Csv(r.Floor), Csv(r.PremiseType),
                Csv(r.TenantName), Csv(r.TenantPhone), Csv(r.RentDisplay), Csv(r.StatusLabel), Csv(r.EndContractDisplay)));
        }

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        return true;
    }

    public static bool ExportPremisesHtml(IEnumerable<LocationsPremiseItem> rows, string title)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "Rapport HTML (*.html)|*.html",
            FileName = $"rapport_locations_{DateTime.Now:yyyyMMdd}.html"
        };
        if (dlg.ShowDialog() != true)
            return false;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>").Append(title).AppendLine("</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,sans-serif;margin:24px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:8px}th{background:#2D6A4F;color:#fff}</style></head><body>");
        sb.Append("<h1>").Append(title).AppendLine("</h1>");
        sb.AppendLine("<table><tr><th>Code</th><th>Nom</th><th>Bâtiment</th><th>Étage</th><th>Type</th><th>Locataire</th><th>Loyer</th><th>Statut</th></tr>");
        foreach (var r in rows)
        {
            sb.Append("<tr><td>").Append(H(r.Code)).Append("</td><td>").Append(H(r.Name))
                .Append("</td><td>").Append(H(r.Building)).Append("</td><td>").Append(H(r.Floor))
                .Append("</td><td>").Append(H(r.PremiseType)).Append("</td><td>").Append(H(r.TenantName))
                .Append("</td><td>").Append(H(r.RentDisplay)).Append("</td><td>").Append(H(r.StatusLabel))
                .AppendLine("</td></tr>");
        }
        sb.AppendLine("</table></body></html>");
        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        return true;
    }

    public static bool PrintPremises(IEnumerable<LocationsPremiseItem> rows, string title)
    {
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(100) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });
        table.Columns.Add(new TableColumn { Width = new GridLength(80) });

        var header = new TableRowGroup();
        var headerRow = new TableRow { Background = System.Windows.Media.Brushes.LightGray, FontWeight = FontWeights.SemiBold };
        foreach (var h in new[] { "Code", "Nom", "Bâtiment", "Étage", "Type", "Locataire", "Loyer", "Statut" })
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(h))) { Padding = new Thickness(6) });
        header.Rows.Add(headerRow);
        table.RowGroups.Add(header);

        var body = new TableRowGroup();
        foreach (var r in rows)
        {
            var row = new TableRow();
            foreach (var v in new[] { r.Code, r.Name, r.Building, r.Floor, r.PremiseType, r.TenantName, r.RentDisplay, r.StatusLabel })
                row.Cells.Add(new TableCell(new Paragraph(new Run(v))) { Padding = new Thickness(6) });
            body.Rows.Add(row);
        }
        table.RowGroups.Add(body);

        var doc = new FlowDocument(new Paragraph(new Run(title)) { FontSize = 18, FontWeight = FontWeights.Bold })
        {
            PagePadding = new Thickness(48),
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 11
        };
        doc.Blocks.Add(table);

        var pd = new PrintDialog();
        if (pd.ShowDialog() != true)
            return false;

        pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, title);
        return true;
    }

    private static string Csv(string value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";

    private static string H(string value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
